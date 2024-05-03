using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitGet.BitGetFutures.Entity
{
    public class ResponseRestMessage<T>
    {
        public string code;
        public string msg;
        public string requestTime;
        public T data;
    }

    public class RestMessageSymbol
    {
        public string symbol;
        public string makerFeeRate;
        public string takerFeeRate;
        public string feeRateUpRatio;
        public string openCostUpRatio;
        public string quoteCoin;
        public string baseCoin;
        public string buyLimitPriceRatio;
        public string sellLimitPriceRatio;
        public List<string> supportMarginCoins;
        public string minTradeNum;
        public string priceEndStep;
        public string volumePlace;
        public string sizeMultiplier;
        public string symbolType;
        public string symbolStatus;
        public string offTime;
        public string limitOpenTime;
        public string maintainTime;
        public string pricePlace;
    }

    public class RestMessageAccount
    {
        public string marginCoin;
        public string locked;
        public string available;
        public string crossMaxAvailable;
        public string fixedMaxAvailable;
        public string maxTransferOut;
        public string equity;
        public string usdtEquity;
        public string btcEquity;
        public string crossRiskRate;
        public string crossMarginLeverage;
        public string fixedLongLeverage;
        public string fixedShortLeverage;
        public string marginMode;
        public string holdMode;
        public string unrealizedPL;
        public string bonus;
    }

    public class RestMessagePositions
    {
        public string marginCoin;
        public string symbol;
        public string holdSide;
        public string openDelegateCount;
        public string margin;
        public string available;
        public string locked;
        public string total;
        public string leverage;
        public string achievedProfits;
        public string averageOpenPrice;
        public string marginMode;
        public string holdMode;
        public string unrealizedPL;
        public string liquidationPrice;
        public string keepMarginRate;
        public string marketPrice;
        public string cTime;
    }

    public class RestMessageOrders
    {
        public string symbol; // ": "BTCUSDT_UMCBL",
        public string size; // ": 0.050,
        public string orderId; // ": "1044911928892862465",
        public string clientOid; // ": "xx005",
        public string filledQty; // ": 0.000,
        public string fee; // ": 0E-8,
        public string price; // ": 25500.00,
        public string state; // ": "new",
        public string side; // ": "open_long",
        public string timeInForce; // ": "normal",
        public string totalProfits; // ": 0E-8,
        public string posSide; // ": "long",
        public string marginCoin; // ": "USDT",
        public string presetTakeProfitPrice; // ": 33800.00,
        public string presetStopLossPrice; // ": 11300.00,
        public string filledAmount; // ": 0.0000,
        public string orderType; // ": "limit",
        public string leverage; // ": "4",
        public string marginMode; // ": "crossed",
        public string reduceOnly; // ": false,
        public string enterPointSource; // ": "API",
        public string tradeSide; // ": "open_long",
        public string holdMode; // ": "double_hold",
        public string orderSource; // ": "normal",
        public string cTime; // ": "1684852338057",
        public string uTime; // ": "1684852338057"
    }

    public class RestOrderStatusResponce
    {
        public string code;

        public RestMessageOrders data;
    }

    public class RestMyTradesResponce
    {
        public string code;

        public string msg; //":"success",

        public List<RestMyTrade> data;
    }

    public class RestMyTrade
    {
        public string tradeId; // ":"802377534023585793",
        public string symbol; // ":"BTCUSDT_UMCBL",
        public string orderId; // ":"802377533381816325",
        public string price; // ":"0",
        public string sizeQty; // ":"0.3247",
        public string fee; // ":"0E-8",
        public string side; // ":"burst_close_long",
        public string fillAmount; // ":"0.3247",
        public string profit; // ":"0E-8",
        public string enterPointSource; // ": "WEB",
        public string tradeSide; // ": "buy_single",
        public string holdMode; // ": "single_hold",
        public string takerMakerFlag; // ": "taker",
        public string ctime; // ":"1627027632241"
    }
}