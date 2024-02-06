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
        public string symbol;
        public string rootSymbol;
        public string type;
        public string firstOpenDate;
        public string expireDate;
        public string settleDate;
        public string baseCurrency;
        public string quoteCurrency;
        public string settleCurrency;
        public string maxOrderQty;
        public string maxPrice;
        public string lotSize;
        public string tickSize;
        public string indexPriceTickSize;
        public string multiplier;
        public string initialMargin;
        public string mastringainMargin;
        public string maxRiskLimit;
        public string minRiskLimit;
        public string riskStep;
        public string makerFeeRate;
        public string takerFeeRate;
        public string takerFixFee;
        public string makerFixFee;
        public string settlementFee;
        public string isDeleverage;
        public string isQuanto;
        public string isInverse;
        public string markMethod;
        public string fairMethod;
        public string fundingBaseSymbol;
        public string fundingQuoteSymbol;
        public string fundingRateSymbol;
        public string indexSymbol;
        public string settlementSymbol;
        public string status;
        public string fundingFeeRate;
        public string predictedFundingFeeRate;
        public string openstringerest;
        public string turnoverOf24h;
        public string volumeOf24h;
        public string markPrice;
        public string indexPrice;
        public string lastTradePrice;
        public string nextFundingRateTime;
        public string maxLeverage;
        public string[] sourceExchanges;
        public string premiumsSymbol1M;
        public string premiumsSymbol8H;
        public string fundingBaseSymbol1M;
        public string fundingQuoteSymbol1M;
        public string lowPrice;
        public string highPrice;
        public string priceChgPct;
        public string priceChg;
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
}