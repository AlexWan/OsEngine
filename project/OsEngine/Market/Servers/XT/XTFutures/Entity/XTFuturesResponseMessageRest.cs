using System.Collections.Generic;


namespace OsEngine.Market.Servers.XT.XTFutures.Entity
{

    public class XTFuturesResponseDepth
    {
        public string timestamp { get; set; } //"1662444177871" in ms
        public string lastUpdateId { get; set; } //"1662444177871" in ms
        public List<List<string>> bids { get; set; } //List of bids
        public List<List<string>> asks { get; set; } //List of asks
    }

    public class XTFuturesResponseToken
    {
        public string accessToken { get; set; } //Private API access token
        public string refreshToken { get; set; }
    }

    public class XTFuturesResponseSymbols
    {
        public string time { get; set; } //1662444177871 in ms,
        public string version { get; set; } //Version number, when the request version number is consistent with the response content version,
                                            //the list will not be returned, reducing IO eg: 2e14d2cd5czcb2c2af2c1db 
        public List<XTFuturesResponseSymbol> symbols { get; set; } //List of symbols
    }

    public class XTFuturesResponseCandles
    {
        public List<XTFuturesResponseCandle> candles { get; set; } //List of candles
    }

    public class XTFuturesResponseCandle
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

    public class XTFuturesResponsePlaceOrder
    {
        public string orderId { get; set; } //Placed Order Id
    }

    public class XTFuturesResponseAsset
    {
        public string currency { get; set; } //Asset currency
        public string currencyId { get; set; } //Asset currency Id
        public string frozenAmount { get; set; } //Frozen amount
        public string availableAmount { get; set; } //Available amount
        public string totalAmount { get; set; } //Total amount
        public string convertBtcAmount { get; set; } //BTC amount
    }

    public class XTFuturesResponseAssets
    {
        public string totalUsdtAmount { get; set; }
        public string totalBtcAmount { get; set; }
        public List<XTFuturesResponseAsset> assets { get; set; } //List of assets
    }

    public class XTFuturesResponseSymbol
    {
        public string id { get; set; } //Id
        public string symbol { get; set; } //"btc_usdt", Symbol
        public string displayName { get; set; } //Display name
        public string state { get; set; } //symbol state [ONLINE;OFFLINE,DELISTED]
        public string tradingEnabled { get; set; } //Trading is available or not
        public string openapiEnabled { get; set; } //Openapi transaction is available or not
        public string nextStateTime { get; set; }
        public string nextState { get; set; }
        public string depthMergePrecision { get; set; } //Depth Merge Accuracy
        public string baseCurrency { get; set; } //"btc"
        public string baseCurrencyPrecision { get; set; } //"8"
        public string baseCurrencyId { get; set; } //"2"
        public string quoteCurrency { get; set; } //"usdt"
        public string quoteCurrencyPrecision { get; set; } //"4"
        public string quoteCurrencyId { get; set; } //"11"
        public string pricePrecision { get; set; } //"4", Transaction price accuracy
        public string quantityPrecision { get; set; } //"6", Transaction quantity accuracy
        public List<string> orderTypes { get; set; } //List of Order Types [LIMIT;MARKET]
        public List<string> timeInForces { get; set; } //List of Effective Ways [GTC, IOC, FOK, GTX=Revoke if unable to become a pending party]
        public string displayWeight { get; set; } //Show the weight, the greater the weight, the more forward 
        public string displayLevel { get; set; } //Presentation level, [FULL=Full display,SEARCH=Search display,DIRECT=Direct display,NONE=Don't show]
        public List<XTFuturesFilter> filters { get; set; } //List of filters
    }

    public class XTFuturesFilter
    {
        public string filter { get; set; }
        public string buyMaxDeviation { get; set; }
        public string sellMaxDeviation { get; set; }
        public string maxDeviation { get; set; }
        public string durationSeconds { get; set; }
        public string maxPriceMultiple { get; set; }
        public string min { get; set; }
        public string max { get; set; }
        public string tickSize { get; set; }
    }

    public class XTFuturesTradeHistoryItem
    {
        public string fee { get; set; }          // Fee
        public string feeCoin { get; set; }       // Currency of fee
        public string orderId { get; set; }         // Order ID
        public string execId { get; set; }          // Trade ID
        public string price { get; set; }        // Price
        public string quantity { get; set; }     // Volume
        public string symbol { get; set; }        // Trading pair
        public string timestamp { get; set; }       // Time
        public string takerMaker { get; set; }    // TAKER or MAKER
    }


    public class XTFuturesOrderResponse
    {
        public string symbol { get; set; } //"symbol": "BTC_USDT",
        public string orderId { get; set; } //"orderId": "6216559590087220004",
        public string clientOrderId { get; set; } //"clientOrderId": "16559590087220001",
        public string baseCurrency { get; set; } //"baseCurrency": "string",
        public string quoteCurrency { get; set; } //"quoteCurrency": "string",
        public string side { get; set; } //"side": "BUY,SELL"
        public string type { get; set; } //"type": "LIMIT,MARKET"
        public string timeInForce { get; set; } //"timeInForce": "GTC,IOC,FOK,GTX"
        public string price { get; set; } //"price": "40000",
        public string origQty { get; set; } //"origQty": "2",
        public string origQuoteQty { get; set; } //"origQuoteQty": "48000",
        public string executedQty { get; set; } //"executedQty": "1.2",
        public string leavingQty { get; set; } //"leavingQty": "The quantity to be executed (if the order is cancelled or the order is rejected, the value is 0)"
        public string tradeBase { get; set; } //"tradeBase": "2", transaction quantity
        public string tradeQuote { get; set; } //"tradeQuote": "48000",
        public string avgPrice { get; set; } //"avgPrice": "42350",
        public string fee { get; set; } //"fee": "string",
        public string feeCurrency { get; set; } //"feeCurrency": "string",
        public string state { get; set; } //"state": "NEW,PARTIALLY_FILLED,FILLED,CANCELED,REJECTED,EXPIRED"
        public string time { get; set; } //"time": 1655958915583,
        public string updateTime { get; set; } //"updatedTime": 1655958915583
    }


    public class XTFuturesOrderHistoryResult
    {
        public string hasNext { get; set; }   // "true"/"false"
        public string hasPrev { get; set; }   // "true"/"false"
        public List<XTFuturesOrderItem> items { get; set; }
    }

    public class XTFuturesOrderResult
    {
        public List<XTFuturesOrderItem> items { get; set; } // List of orders
        public string page { get; set; }                   // Current page
        public string ps { get; set; }                     // Page size
        public string total { get; set; }                  // Total count
    }

    public class XTFuturesOrderItem
    {
        public string orderId { get; set; }          // order id
        public string clientOrderId { get; set; }    // client order id
        public string symbol { get; set; }           // trading pair
        public string contractSize { get; set; }     // contract size
        public string orderType { get; set; }        // order type (LIMIT, MARKET)
        public string orderSide { get; set; }        // order side (BUY, SELL)
        public string positionSide { get; set; }     // position side (LONG, SHORT)
        public string positionType { get; set; }     // position type (CROSSED, ISOLATED)
        public string timeInForce { get; set; }      // time in force (GTC, IOC, etc.)
        public string closePosition { get; set; }    // whether close all (true/false)
        public string price { get; set; }            // order price
        public string origQty { get; set; }          // original quantity
        public string avgPrice { get; set; }         // average deal price
        public string executedQty { get; set; }      // executed quantity
        public string marginFrozen { get; set; }     // frozen margin
        public string remark { get; set; }           // remark (nullable)
        public string sourceId { get; set; }         // source id (nullable)
        public string sourceType { get; set; }       // source type
        public string forceClose { get; set; }       // is forced close (true/false)
        public string leverage { get; set; }         // leverage
        public string openPrice { get; set; }        // open price (nullable)
        public string closeProfit { get; set; }      // close profit (nullable)
        public string state { get; set; }            // order state (NEW, CANCELED, FILLED, etc.)
        public string createdTime { get; set; }      // creation timestamp (ms)
        public string updatedTime { get; set; }      // last update timestamp (ms)
        public string welfareAccount { get; set; }   // welfare account flag
        public string triggerPriceType { get; set; } // trigger price type (nullable)
        public string triggerProfitPrice { get; set; } // stop profit price (nullable)
        public string profitDelegateOrderType { get; set; } // profit delegate order type (nullable)
        public string profitDelegateTimeInForce { get; set; } // profit delegate time in force (nullable)
        public string profitDelegatePrice { get; set; } // profit delegate price (nullable)
        public string triggerStopPrice { get; set; } // stop loss price (nullable)
        public string stopDelegateOrderType { get; set; } // stop delegate order type (nullable)
        public string stopDelegateTimeInForce { get; set; } // stop delegate time in force (nullable)
        public string stopDelegatePrice { get; set; } // stop delegate price (nullable)
        public string markPrice { get; set; }        // mark price
        public string desc { get; set; }             // description
        public string systemCancel { get; set; }     // system cancel flag (true/false)
        public string profit { get; set; }           // profit flag (true/false)
    }
    public class XTFuturesOrderById
    {
        public string clientOrderId { get; set; }     // Client order ID
        public string avgPrice { get; set; }         // Average price
        public string closePosition { get; set; }    // Whether to close all when order condition is triggered
        public string closeProfit { get; set; }      // Offset profit and loss
        public string createdTime { get; set; }      // Create time
        public string executedQty { get; set; }      // Executed volume (Cont)
        public string forceClose { get; set; }       // Is it a liquidation order
        public string marginFrozen { get; set; }     // Occupied margin
        public string orderId { get; set; }          // Order ID
        public string orderSide { get; set; }        // Order side (BUY/SELL)
        public string orderType { get; set; }        // Order type (LIMIT/MARKET)
        public string origQty { get; set; }          // Original quantity (Cont)
        public string positionSide { get; set; }     // Position side (LONG/SHORT)
        public string price { get; set; }            // Order price
        public string sourceId { get; set; }         // Triggering condition ID
        public string state { get; set; }            // Order state: NEW, PARTIALLY_FILLED, FILLED, etc.
        public string symbol { get; set; }           // Trading pair
        public string timeInForce { get; set; }      // Time in force (GTC/IOC/FOK/GTX)
        public string triggerProfitPrice { get; set; } // Take profit trigger price
        public string triggerStopPrice { get; set; }   // Stop loss trigger price
    }

    public class ListenKeyResponse
    {
        public string returnCode { get; set; }      
        public string msgInfo { get; set; }
        public ApiError error { get; set; }
        public string result { get; set; }           
    }

    public class ApiError
    {
        public string code { get; set; }
        public string msg { get; set; }
    }

    public class XTFuturesWsOrderMessage
    {
        public string topic { get; set; }  // Topic name, e.g. "order"
        public string @event { get; set; } // Event name, e.g. "order@123456"
        public XTFuturesWsOrderData data { get; set; } // Order data
    }

    public class XTFuturesWsOrderData
    {
        public string symbol { get; set; }        // Trading pair
        public string orderId { get; set; }       // Order Id
        public string origQty { get; set; }       // Original Quantity
        public string avgPrice { get; set; }      // Average price
        public string price { get; set; }         // Price
        public string executedQty { get; set; }   // Volume (Cont)
        public string orderSide { get; set; }     // BUY, SELL
        public string timeInForce { get; set; }   // Valid way
        public string positionSide { get; set; }  // LONG, SHORT
        public string marginFrozen { get; set; }  // Occupied margin
        public string sourceType { get; set; }    // DEFAULT, ENTRUST, PROFIT
        public string type { get; set; }          // ORDER
        public string state { get; set; }         // Order state (NEW, FILLED, etc.)
        public string createdTime { get; set; }   // Create time (timestamp)
        public string leverage { get; set; }      // Leverage
        public string positionType { get; set; }  // Position type (CROSSED, ISOLATED)
        public string orderType { get; set; }     // Order type (LIMIT, MARKET)
    }

}
