using System.Collections.Generic;

namespace OsEngine.Market.Servers.KuCoin.KuCoinFutures.Json
{
    public class ResponseMessageRest<T>
    {
        public string code;
        public string msg;
        public T data;
    }

    public class ResponsePrivateWebSocketConnection
    {
        public string code;

        public class InstanceServer
        {
            public string endpoint;
            public string encrypt;
            public string protocol;
            public string pinginterval;
            public string pingTimeout;
        }

        public class WSData
        {
            public string token;
            public List<InstanceServer> instanceServers;
        }

        public WSData data;
    }

    public class ResponsePlaceOrder
    {
        public string orderId;
    }

    public class ResponseAllOrders
    {
        public string currentPage;
        public string pageSize;
        public string totalNum;
        public string totalPage;

        public List<ResponseOrder> items;
    }

    public class ResponseOrder
    {
        public string id { get; set; } // Order ID
        public string symbol { get; set; } // Symbol of the contract
        public string updatedAt { get; set; } // last update time
        public string timeInForce { get; set; } // Time in force policy type 
        public string stp { get; set; } // Self trade prevention types
        public string stopTriggered { get; set; } // Mark to show whether the stop order is triggered
        public string remark { get; set; } // Remark of the order
        public string filledValue { get; set; } // Value of the executed orders
        public string clientOid { get; set; } // Unique order id created by users to identify their orders
        public string forceHold { get; set; } // A mark to forcely hold the funds for an order
        public string dealSize { get; set; } // Executed quantity
        public string endAt { get; set; } // End time
        public string dealValue { get; set; } // Executed size of funds
        public string hidden { get; set; } // Mark of the hidden order
        public string reduceOnly { get; set; } // A mark to reduce the position size only
        public string cancelExist { get; set; } // Mark of the canceled orders
        public string settleCurrency { get; set; } // settlement currency
        public string type { get; set; } // Order type, market order or limit order
        public string value { get; set; } // Order value
        public string closeOrder { get; set; } // A mark to close the position
        public string stop { get; set; } // Stop order type (stop limit or stop market)
        public string createdAt { get; set; } // Time the order created
        public string isActive { get; set; } // Mark of the active orders
        public string status { get; set; } // order status: “open” or “done”
        public string price { get; set; } // Order price
        public string stopPriceType { get; set; } // Trigger price type of stop orders
        public string leverage { get; set; } // Leverage of the order
        public string tags { get; set; } // Tag order source
        public string size { get; set; } // Order quantity
        public string visibleSize { get; set; } // Visible size of the iceberg order
        public string stopPrice { get; set; } // Trigger price of stop orders
        public string postOnly { get; set; } // Mark of post only
        public string orderTime { get; set; } // order create time in nanosecond
        public string iceberg { get; set; } // Mark of the iceberg order
        public string side { get; set; } // Transaction side
        public string filledSize { get; set; } // Executed order quantity
        public string marginMode { get; set; } // Added field for margin mode: ISOLATED (isolated), CROSS (cross margin).
    }

    public class Ticker
    {
        public string sequence;
        public string symbol;
        public string price;
        public string size;
        public string bestAsk;
        public string bestAskSize;
        public string bestBid;
        public string bestBidSize;
        public string time;
    }

    public class ResponseAsset
    {
        public string accountEquity; //99.8999305281, 	//Account equity = marginBalance + Unrealised PNL
        public string unrealisedPNL; //0, 				//Unrealised profit and loss
        public string marginBalance; //99.8999305281, 	//Margin balance = positionMargin + orderMargin + frozenFunds + availableBalance - unrealisedPNL
        public string positionMargin; //0, 				//Position margin
        public string orderMargin; //0, 				//Order margin
        public string frozenFunds; //0, 				//Frozen funds for withdrawal and out-transfer
        public string availableBalance; //99.8999305281 //Available balance
        public string currency; //"XBT" 				//currency code
    }

    public class ResponsePosition
    {
        public string id; //"5e81a7827911f40008e80715",               //Position ID
        public string symbol; //"XBTUSDTM",                           //Symbol
        public string autoDeposit; //False, 							//Auto deposit margin or not
        public string maintMarginReq; //0.005, 						//Maintenance margin requirement
        public string riskLimit; //2000000, 							//Risk limit
        public string realLeverage; //5.0, 							//Leverage o the order
        public string crossMode; //False, 							//Cross mode or not
        public string delevPercentage; //0.35, 						//ADL ranking percentile
        public string openingTimestamp; //1623832410892, 				//Open time
        public string currentTimestamp; //1623832488929, 				//Current timestamp
        public string currentQty; //1, 								//Current postion quantity
        public string currentCost; //40.008, 							//Current postion value
        public string currentComm; //0.0240048, 						//Current commission
        public string unrealisedCost; //40.008, 						//Unrealised value
        public string realisedGrossCost; //0.0, 						//Accumulated realised gross profit value
        public string realisedCost; //0.0240048, 						//Current realised position value
        public string isOpen; //True, 								//Opened position or not
        public string markPrice; //40014.93, 							//Mark price
        public string markValue; //40.01493, 							//Mark value
        public string posCost; //40.008, 								//Position value
        public string posCross; //0.0, 								//added margin
        public string posInit; //8.0016, 								//Leverage margin
        public string posComm; //0.02880576, 							//Bankruptcy cost
        public string posLoss; //0.0, 								//Funding fees paid out
        public string posMargin; //8.03040576, 						//Position margin
        public string posMaint; //0.23284656, 						//Maintenance margin
        public string maintMargin; //8.03733576, 						//Position margin
        public string realisedGrossPnl; //0.0, 						//Accumulated realised gross profit value
        public string realisedPnl; //-0.0240048, 						//Realised profit and loss
        public string unrealisedPnl; //0.00693, 						//Unrealised profit and loss
        public string unrealisedPnlPcnt; //0.0002, 					//Profit-loss ratio of the position
        public string unrealisedRoePcnt; //0.0009, 					//Rate of return on investment
        public string avgEntryPrice; //40008.0, 						//Average entry price
        public string liquidationPrice; //32211.0, 					//Liquidation price
        public string bankruptPrice; //32006.0, 						//Bankruptcy price
        public string settleCurrency; //"USDT", 						//Currency used to clear and settle the trades
        public string isInverse; //False,  							//Reverse contract or not
        public string userId; //1234321123,							//userid
        public string maintainMargin; //0.005  						//Maintenance margin requirement
    }

    // https://www.kucoin.com/docs/rest/futures-trading/market-data/get-symbols-list
    public class ResponseSymbol
    {
        public string symbol { get; set; }
        public string rootSymbol { get; set; }
        public string type { get; set; }
        public string firstOpenDate { get; set; }
        public string expireDate { get; set; }
        public string settleDate { get; set; }
        public string baseCurrency { get; set; }
        public string quoteCurrency { get; set; }
        public string settleCurrency { get; set; }
        public string maxOrderQty { get; set; }
        public string maxPrice { get; set; }
        public string lotSize { get; set; }
        public string tickSize { get; set; }
        public string indexPriceTickSize { get; set; }
        public string multiplier { get; set; }
        public string initialMargin { get; set; }
        public string maintainMargin { get; set; }
        public string maxRiskLimit { get; set; }
        public string minRiskLimit { get; set; }
        public string riskStep { get; set; }
        public string makerFeeRate { get; set; }
        public string takerFeeRate { get; set; }
        public string takerFixFee { get; set; }
        public string makerFixFee { get; set; }
        public string settlementFee { get; set; }
        public string isDeleverage { get; set; }
        public string isQuanto { get; set; }
        public string isInverse { get; set; }
        public string markMethod { get; set; }
        public string fairMethod { get; set; }
        public string fundingBaseSymbol { get; set; }
        public string fundingQuoteSymbol { get; set; }
        public string fundingRateSymbol { get; set; }
        public string indexSymbol { get; set; }
        public string settlementSymbol { get; set; }
        public string status { get; set; }
        public string fundingFeeRate { get; set; }
        public string predictedFundingFeeRate { get; set; }
        public string fundingRateGranularity { get; set; }
        public string openInterest { get; set; }
        public string turnoverOf24h { get; set; }
        public string volumeOf24h { get; set; }
        public string markPrice { get; set; }
        public string indexPrice { get; set; }
        public string lastTradePrice { get; set; }
        public string nextFundingRateTime { get; set; }
        public string maxLeverage { get; set; }
        public List<string> sourceExchanges { get; set; }
        public string premiumsSymbol1M { get; set; }
        public string premiumsSymbol8H { get; set; }
        public string fundingBaseSymbol1M { get; set; }
        public string fundingQuoteSymbol1M { get; set; }
        public string fundingRateCap { get; set; }
        public string fundingRateFloor { get; set; }
        public string nextFundingRateDateTime { get; set; }
        public string lowPrice { get; set; }
        public string highPrice { get; set; }
        public string priceChgPct { get; set; }
        public string priceChg { get; set; }
        public string k { get; set; }
        public string m { get; set; }
        public string f { get; set; }
        public string mmrLimit { get; set; }
        public string mmrLevConstant { get; set; }
        public string supportCross { get; set; }
        public string buyLimit { get; set; }
        public string sellLimit { get; set; }
    }

    public class ResponseMyTrade
    {
        public string symbol; //"XBTUSDM", //Symbol of the contract
        public string tradeId; // 5ce24c1f0c19fc3c58edc47c , //Trade ID
        public string orderId; // 5ce24c16b210233c36ee321d , // Order ID
        public string side; // sell , //Transaction side
        public string liquidity; // taker , //Liquidity- taker or maker
        public string forceTaker; //true, //Whether to force processing as a taker
        public string price; // 8302 , //Filled price
        public string size; //10, //Filled amount
        public string value; // 0.001204529 , //Order value
        public string feeRate; // 0.0005 , //Floating fees
        public string fixFee; // 0.00000006 , //Fixed fees
        public string feeCurrency; // XBT, //Charging currency
        public string stop; //  , //A mark to the stop order type
        public string fee; // 0.0000012022, //Transaction fee
        public string orderType; // limit, //Order type
        public string tradeType; // trade, //Trade type (trade, liquidation, ADL or settlement)
        public string createdAt; //1558334496000, //Time the order created
        public string settleCurrency; // XBT, //settlement currency
        public string openFeePay; // 0.002 ,
        public string closeFeePay; // 0.002 ,
        public string tradeTime; //1558334496000000000 //trade time in nanosecond
    }

    public class ResponseMyTrades
    {
        public string currentPage;
        public string pageSize;
        public string totalNum;
        public string totalPage;

        public List<ResponseMyTrade> items;
    }

    public class FundingItemHistory
    {
        public string symbol { get; set; }
        public string fundingRate { get; set; }
        public string timepoint { get; set; }
    }
}