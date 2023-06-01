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
}
