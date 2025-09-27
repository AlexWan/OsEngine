using System.Collections.Generic;

namespace OsEngine.Market.Servers.OKX.Entity
{
    public class ResponseRestMessage<T>
    {
        public string code { get; set; }
        public string msg { get; set; }
        public T data { get; set; }
        public string inTime { get; set; }
        public string outTime { get; set; }
    }

    public class RestMessageSendOrder
    {
        public string sMsg;
    }

    public class RestMessageCandle
    {
        public List<List<string>> data { get; set; }
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
        public List<EntrustedList> entrustedList;
    }

    public class EntrustedList
    {
        public string symbol;
        public string baseVolume;
        public string orderId;
        public string clientOid;
        public string filledQty;
        public string fee;
        public string price;
        public string status;
        public string side;
        public string timeInForce;
        public string totalProfits;
        public string posSide;
        public string marginCoin;
        public string presetTakeProfitPrice;
        public string presetStopLossPrice;
        public string filledAmount;
        public string orderType;
        public string leverage;
        public string marginMode;
        public string size;
        public string holdMode;
        public string tradeSide;
        public string cTime;
        public string uTime;
    }

    public class DataOrderStatus
    {
        public string symbol;
        public string size;
        public string orderId;
        public string clientOid;
        public string price;
        public string state;
        public string side;
        public string posSide;
        public string posMode;
        public string orderType;
        public string cTime;
        public string marginCoin;
    }

    public class RestMyTradesResponce
    {
        public string code;

        public string msg;

        public DataMyTrades data;
    }

    public class DataMyTrades
    {
        public List<FillList> fillList;
    }
    public class FillList
    {
        public string tradeId;
        public string symbol;
        public string orderId;
        public string price;
        public string baseVolume;
        public string fee;
        public string side;
        public string fillAmount;
        public string profit;
        public string enterPointSource;
        public string tradeSide;
        public string holdMode;
        public string takerMakerFlag;
        public string cTime;
    }

    public class FundingItemHistory
    {
        public string formulaType { get; set; }
        public string fundingRate { get; set; }
        public string fundingTime { get; set; }
        public string instId { get; set; }
        public string instType { get; set; }
        public string method { get; set; }
        public string realizedRate { get; set; }
    }
}