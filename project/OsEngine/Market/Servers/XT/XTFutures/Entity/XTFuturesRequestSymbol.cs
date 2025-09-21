using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OsEngine.Market.Servers.XT.XTFutures.Entity
{

    public class XTFuturesResponseRest<T>  //XTBasicResponse
    {
        public string returnCode { get; set; }
        public string msgInfo { get; set; }
        public XTFuturesApiError error { get; set; }   // может быть null
        public T result { get; set; }
    }
    public class XTFuturesResponseRestNew<T>
    {
        public string rc { get; set; }  // Return code ("0" = success)
        public string mc { get; set; }  // Message code (e.g., "SUCCESS")
        public List<string> ma { get; set; }  // Message args / extra info (may be empty)
        public T result { get; set; }   // Payload
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
        public string id { get; set; }  // Symbol ID
        public string symbol { get; set; }  // Symbol name (e.g. btc_usdt)
        public string symbolGroupId { get; set; }  // Symbol group ID
        public string pair { get; set; }  // Trading pair
        public string contractType { get; set; }  // Contract type (PERPETUAL, DELIVERY)
        public string productType { get; set; }  // Product type (perpetual, futures)
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
        public string walletBalance { get; set; }   // Wallet balance
        public string openOrderMarginFrozen { get; set; } // Frozen due to open orders
        public string isolatedMargin { get; set; }  // Isolated margin
        public string crossedMargin { get; set; }   // Cross margin
        public string amount { get; set; }          // Net asset balance
        public string totalAmount { get; set; }     // Margin balance
        public string convertBtcAmount { get; set; }    // Wallet balance converted to BTC
        public string convertUsdtAmount { get; set; }   // Wallet balance converted to USDT
        public string profit { get; set; }          // Realized PnL
        public string notProfit { get; set; }       // Unrealized PnL
        public string bonus { get; set; }           // Bonus / trial funds
        public string coupon { get; set; }          // Coupon deduction
    }

    public class XTFuturesOrderDetailById
    {
        public string avgPrice { get; set; }          // Average price
        public string closePosition { get; set; }        // Whether to close all when order condition is triggered
        public string closeProfit { get; set; }       // Offset profit and loss
        public string createdTime { get; set; }          // Create time (unix ms)
        public string executedQty { get; set; }       // Executed quantity
        public string forceClose { get; set; }           // Is it a liquidation order
        public string marginFrozen { get; set; }      // Occupied margin
        public string orderId { get; set; }              // Order ID
        public string orderSide { get; set; }          // Order side (Buy/Sell)
        public string orderType { get; set; }          // Order type (Limit/Market)
        public string origQty { get; set; }           // Original quantity
        public string positionSide { get; set; }       // Position side (string/Short)
        public string price { get; set; }             // Order price
        public string sourceId { get; set; }             // Triggering conditions ID
        public string state { get; set; }              // Order state
        public string symbol { get; set; }             // Trading pair
        public string timeInForce { get; set; }        // Valid type
        public string triggerProfitPrice { get; set; }// TP trigger price
        public string triggerStopPrice { get; set; }  // SL trigger price
    }
}

