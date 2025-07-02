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
        public string minTradeUSDT;
        public string maxSymbolOrderNum;
        public string maxProductOrderNum;
        public string maxPositionNum;
        public string symbolStatus;
        public string offTime;
        public string limitOpenTime;
        public string maintainTime;
        public string pricePlace;
    }

    public class RestMessagePositions
    {
        public string marginCoin;
        public string symbol;
        public string holdSide;
        public string openDelegateCount;
        public string marginSize;
        public string available;
        public string locked;
        public string total;
        public string leverage;
        public string achievedProfits;
        public string openPriceAvg;
        public string posMode;
        public string unrealizedPL;
        public string liquidationPrice;
        public string keepMarginRate;
        public string markPrice;
        public string breakEvenPrice;
        public string totalFee;
        public string deductedFee;
        public string marginRatio;
        public string assetMode;
        public string uTime;
        public string autoMargin;
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

    public class Account
    {
        public string marginCoin { get; set; } // Margin coin
        public string locked { get; set; } // Locked quantity (margin coin)
        public string available { get; set; } // Available quantity in the account
        public string crossedMaxAvailable { get; set; } // Maximum available balance to open positions under the cross margin mode (margin coin)
        public string isolatedMaxAvailable { get; set; } // Maximum available balance to open positions under the isolated margin mode (margin coin)
        public string maxTransferOut { get; set; } // Maximum transferable amount
        public string accountEquity { get; set; } // Account equity (margin coin), Includes unrealized PnL(based on mark price)
        public string usdtEquity { get; set; } // Account equity in USDT
        public string btcEquity { get; set; } // Account equity in BTC
        public string crossedRiskRate { get; set; } // Risk ratio in cross margin mode
        public string unrealizedPL { get; set; } // Unrealized PnL
        public string coupon { get; set; } // Trading bonus
        public string unionTotalMargin { get; set; } // Multi-assets
        public string unionAvailable { get; set; } // Total available
        public string unionMm { get; set; } // Maintenance margin
        public List<Asset> assetList { get; set; } // Asset list
        public string isolatedMargin { get; set; } // Isolated Margin Occupied
        public string crossedMargin { get; set; } // Crossed Margin Occupied
        public string crossedUnrealizedPL { get; set; } // unrealizedPL for croessed
        public string isolatedUnrealizedPL { get; set; } // unrealizedPL for isolated
        public string assetMode { get; set; } // Assets mode union Multi-assets mode single Single-assets mode
    }

    public class Asset
    {
        public string Coin { get; set; } // Asset
        public string Balance { get; set; } // Balance
    }

    public class TradeData
    {
        public string tradeId { get; set; }
        public string price { get; set; }
        public string size { get; set; }
        public string side { get; set; }
        public string ts { get; set; }
        public string symbol { get; set; }
    }

    public class OIData
    {
        public List<OpenInterestItem> openInterestList { get; set; }
        public string ts { get; set; }
    }

    public class OpenInterestItem
    {
        public string symbol { get; set; }
        public string size { get; set; }
    }

    public class FundingItem
    {
        public string symbol { get; set; }
        public string fundingRate { get; set; }
        public string fundingRateInterval { get; set; }
        public string nextUpdate { get; set; }
        public string minFundingRate { get; set; }
        public string maxFundingRate { get; set; }
    }

    public class FundingItemHistory
    {
        public string symbol { get; set; }
        public string fundingRate { get; set; }
        public string fundingTime { get; set; }
    }
}