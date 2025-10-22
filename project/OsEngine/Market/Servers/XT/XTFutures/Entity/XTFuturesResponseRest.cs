using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OsEngine.Market.Servers.XT.XTFutures.Entity
{
    public class XTFuturesResponseRest<T>
    {
        public string returnCode { get; set; }
        public string msgInfo { get; set; }
        public XTFuturesApiError error { get; set; }   // may be null
        public T result { get; set; }
    }

    public class XTFuturesResponseRestNew<T>
    {
        public string rc { get; set; }       // Return code ("0" = success)
        public string mc { get; set; }       // Message code (e.g., "SUCCESS")
        public List<string> ma { get; set; }  // Message args / extra info (may be empty)
        public T result { get; set; }        // Payload
    }

    public class XTFuturesApiError
    {
        public string code { get; set; }
        public string msg { get; set; }
    }

    public class XTFuturesSymbolListResult
    {
        public string time { get; set; }
        public string version { get; set; }
        public List<XTFuturesSymbol> symbols { get; set; }
    }

    public class XTFuturesSymbol
    {
        public string id { get; set; }                // Symbol ID
        public string symbol { get; set; }            // Symbol name (e.g. btc_usdt)
        public string symbolGroupId { get; set; }     // Symbol group ID
        public string pair { get; set; }              // Trading pair
        public string contractType { get; set; }      // Contract type (PERPETUAL, DELIVERY)
        public string productType { get; set; }       // Product type (perpetual, futures)
        public string predictEventType { get; set; }  // Prediction event type
        public string predictEventParam { get; set; }  // Prediction event parameters
        public string predictEventSort { get; set; }   // Prediction event sorting
        public string underlyingType { get; set; }     // Underlying type (e.g. U_BASED)
        public string contractSize { get; set; }       // Contract size
        public string tradeSwitch { get; set; }        // Trade switch (true = enabled)
        public string openSwitch { get; set; }         // Open position switch (true = enabled)
        public string isDisplay { get; set; }          // Whether to display this symbol
        public string isOpenApi { get; set; }          // Whether OpenAPI is supported
        public string state { get; set; }              // State (0 = online, etc.)
        public string initLeverage { get; set; }       // Initial leverage
        public string initPositionType { get; set; }   // Initial position type (CROSSED, ISOLATED)
        public string baseCoin { get; set; }           // Base coin (e.g. BTC)
        public string spotCoin { get; set; }           // Spot coin
        public string quoteCoin { get; set; }          // Quote coin (e.g. USDT)
        public string baseCoinPrecision { get; set; }  // Base coin precision
        public string baseCoinDisplayPrecision { get; set; }  // Display precision for base coin
        public string quoteCoinPrecision { get; set; }  // Quote coin precision
        public string quoteCoinDisplayPrecision { get; set; }  // Display precision for quote coin
        public string quantityPrecision { get; set; }   // Quantity precision
        public string pricePrecision { get; set; }      // Price precision
        public string supportOrderType { get; set; }    // Supported order types
        public string supportTimeInForce { get; set; }  // Supported time-in-force values
        public string supportEntrustType { get; set; }  // Supported entrust types
        public string supportPositionType { get; set; } // Supported position types
        public string minQty { get; set; }              // Minimum order quantity
        public string minNotional { get; set; }         // Minimum notional value
        public string maxNotional { get; set; }         // Maximum notional value
        public string multiplierDown { get; set; }      // Multiplier down
        public string multiplierUp { get; set; }        // Multiplier up
        public string maxOpenOrders { get; set; }       // Max open orders
        public string maxEntrusts { get; set; }         // Max entrusts
        public string makerFee { get; set; }            // Maker fee
        public string takerFee { get; set; }            // Taker fee
        public string liquidationFee { get; set; }      // Liquidation fee
        public string marketTakeBound { get; set; }     // Market take bound
        public string depthPrecisionMerge { get; set; } // Depth precision merge
        public List<string> labels { get; set; }        // Labels (e.g. HOT)
        public string onboardDate { get; set; }         // Onboard date (timestamp)
        public string enName { get; set; }              // English name
        public string cnName { get; set; }              // Chinese name
        public string minStepPrice { get; set; }        // Minimum step price
        public string minPrice { get; set; }            // Minimum price
        public string maxPrice { get; set; }            // Maximum price
        public string deliveryDate { get; set; }        // Delivery date
        public string deliveryPrice { get; set; }       // Delivery price
        public string deliveryCompletion { get; set; }  // Delivery completion flag
        public string cnDesc { get; set; }              // Chinese description
        public string enDesc { get; set; }              // English description
        public string cnRemark { get; set; }            // Chinese remark
        public string enRemark { get; set; }            // English remark
        public List<string> plates { get; set; }        // Plates (IDs)
        public string fastTrackCallbackRate1 { get; set; } // Fast track callback rate 1
        public string fastTrackCallbackRate2 { get; set; } // Fast track callback rate 2
        public string minTrackCallbackRate { get; set; }   // Minimum track callback rate
        public string maxTrackCallbackRate { get; set; }   // Maximum track callback rate
        public string latestPriceDeviation { get; set; }   // Latest price deviation
        public string marketOpenTakeBound { get; set; }    // Market open take bound
        public string marketCloseTakeBound { get; set; }   // Market close take bound
        public string offTime { get; set; }                // Off time
        public string updatedTime { get; set; }            // Updated time
        public string displaySwitch { get; set; }          // Display switch (true = enabled)
        public string curMaxLeverage { get; set; }         // Current max leverage
        public string riskNominalValueCoefficient { get; set; } // Risk nominal value coefficient
        public string riskExpireTime { get; set; }
    }

    public class XTFuturesBalance
    {
        public string accountId { get; set; }  // Account ID
        public string userId { get; set; }     // User ID
        public string coin { get; set; }       // Currency (e.g., usdt)
        public string underlyingType { get; set; }  // Coin standard (1 = coin-margined, 2 = usdt-margined)
        public string walletBalance { get; set; }   // Currency balance
        public string openOrderMarginFrozen { get; set; } // Order frozen
        public string isolatedMargin { get; set; }  //  Margin freeze
        public string crossedMargin { get; set; }   // Full margin freeze
        public string amount { get; set; }          // Net asset balance
        public string totalAmount { get; set; }     // Margin balance
        public string convertBtcAmount { get; set; }    // Wallet balance converted to BTC
        public string convertUsdtAmount { get; set; }   // Wallet balance converted to USDT
        public string profit { get; set; }          // Realized PnL(Profit and loss)
        public string notProfit { get; set; }       // Unrealized PnL
        public string bonus { get; set; }           // Bonus / trial funds
        public string coupon { get; set; }          // Coupon deduction
    }

    public class XTFuturesPosition
    {
        public string autoMargin;            // Whether to automatically call margin (true/false as string)
        public string availableCloseSize;    // Available quantity (Cont)
        public string breakPrice;            // Blowout price
        public string calMarkPrice;          // Calculated mark price
        public string closeOrderSize;        // Quantity of open order (Cont)
        public string contractType;          // Contract type: PERPETUAL / PREDICT
        public string entryPrice;            // Average opening price
        public string floatingPL;            // Unrealized profit or loss
        public string isolatedMargin;        // Isolated margin
        public string leverage;              // Leverage ratio
        public string openOrderMarginFrozen; // Margin frozen by open orders
        public string openOrderSize;         // Opening orders occupied (Cont)
        public string positionSide;          // Position side (e.g., LONG/SHORT)
        public string positionSize;          // Position quantity (Cont)
        public string positionType;          // Position type: CROSSED / ISOLATED
        public string profitId;              // Take profit / stop loss id
        public string realizedProfit;        // Realized profit and loss
        public string symbol;                // Trading pair (symbol)
        public string triggerPriceType;      // Trigger price type: 1 Index, 2 Mark, 3 Last
        public string triggerProfitPrice;    // Take profit trigger price
        public string triggerStopPrice;      // Stop loss trigger price
        public string welfareAccount;        // Welfare account flag (true/false as string)
    }

    public class XTFuturesMyTrade
    {
        public string symbol { get; set; }        // symbol, e.g. "gmt_usdt"
        public string orderSide { get; set; }     // BUY / SELL
        public string positionSide { get; set; }  // LONG / SHORT (affects position sign, not buy/sell)
        public string orderId { get; set; }       // order identifier
        public string price { get; set; }         // execution price
        public string quantity { get; set; }      // execution quantity
        public string isMaker { get; set; }       // maker flag ("true"/"false" as string)
        public string marginUnfrozen { get; set; }// margin released
        public string fee { get; set; }           // fee paid
        public string timestamp { get; set; }     // timestamp in milliseconds
        public string clientOrderId { get; set; } //client order id 
    }

    public class XTFuturesTradeHistoryResult
    {
        public List<XTFuturesTradeHistory> items { get; set; }
        public string page { get; set; }
        public string ps { get; set; }
        public string total { get; set; }
    }

    public class XTFuturesTradeHistory
    {
        public string orderId { get; set; }
        public string execId { get; set; }
        public string symbol { get; set; }
        public string contractSize { get; set; }
        public string quantity { get; set; }
        public string price { get; set; }
        public string fee { get; set; }
        public string couponDeductFee { get; set; }
        public string bonusDeductFee { get; set; }
        public string feeCoin { get; set; }
        public string timestamp { get; set; }
        public string takerMaker { get; set; }
        public string orderSide { get; set; }
        public string positionSide { get; set; }
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
        public string orderId { get; set; }                   // order id
        public string clientOrderId { get; set; }             // client order id
        public string symbol { get; set; }                    // trading pair
        public string contractSize { get; set; }              // contract size
        public string orderType { get; set; }                 // order type (LIMIT, MARKET)
        public string orderSide { get; set; }                 // order side (BUY, SELL)
        public string positionSide { get; set; }              // position side (LONG, SHORT)
        public string positionType { get; set; }              // position type (CROSSED, ISOLATED)
        public string timeInForce { get; set; }               // time in force (GTC, IOC, etc.)
        public string closePosition { get; set; }             // whether close all (true/false)
        public string price { get; set; }                     // order price
        public string origQty { get; set; }                   // original quantity
        public string avgPrice { get; set; }                  // average deal price
        public string executedQty { get; set; }               // executed quantity
        public string marginFrozen { get; set; }              // frozen margin
        public string remark { get; set; }                    // remark (nullable)
        public string sourceId { get; set; }                  // source id (nullable)
        public string sourceType { get; set; }                // source type
        public string forceClose { get; set; }                // is forced close (true/false)
        public string leverage { get; set; }                  // leverage
        public string openPrice { get; set; }                 // open price (nullable)
        public string closeProfit { get; set; }               // close profit (nullable)
        public string state { get; set; }                     // order state (NEW, CANCELED, FILLED, etc.)
        public string createdTime { get; set; }               // creation timestamp (ms)
        public string updatedTime { get; set; }               // last update timestamp (ms)
        public string welfareAccount { get; set; }            // welfare account flag
        public string triggerPriceType { get; set; }          // trigger price type (nullable)
        public string triggerProfitPrice { get; set; }        // stop profit price (nullable)
        public string profitDelegateOrderType { get; set; }   // profit delegate order type (nullable)
        public string profitDelegateTimeInForce { get; set; } // profit delegate time in force (nullable)
        public string profitDelegatePrice { get; set; }       // profit delegate price (nullable)
        public string triggerStopPrice { get; set; }          // stop loss price (nullable)
        public string stopDelegateOrderType { get; set; }     // stop delegate order type (nullable)
        public string stopDelegateTimeInForce { get; set; }   // stop delegate time in force (nullable)
        public string stopDelegatePrice { get; set; }         // stop delegate price (nullable)
        public string markPrice { get; set; }                 // mark price
        public string desc { get; set; }                      // description
        public string systemCancel { get; set; }              // system cancel flag (true/false)
        public string profit { get; set; }                    // profit flag (true/false)
    }

    public class XTFuturesSendOrder
    {
        public string clientOrderId { get; set; }            //client order id
        public string symbol { get; set; }                   //"btc_usdt", symbol
        public string orderSide { get; set; }                //BUY,SELL
        public string orderType { get; set; }                //order type:LIMIT,MARKET
        public string origQty { get; set; }                  //Quantity (Cont)
        public string price { get; set; }                    //price.
        public string timeInForce { get; set; }              //Valid way:GTC;IOC;FOK;GTX
        public string positionSide { get; set; }             //Position side:LONG;SHORT   
        public string triggerProfitPrice { get; set; }       //Stop profit price
        public string triggerStopPrice { get; set; }         //Stop loss price
    }

    public class XTFuturesCancelAllOrders
    {
        public string symbol { get; set; }                   //"btc_usdt", symbol
        public string bizType { get; set; }                  //SPOT, LEVER                                                 
    }
}