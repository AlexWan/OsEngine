/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitGetUnified.Entity
{
    public class BitGetUnifiedResponse<T>
    {
        public string code;
        public string msg;
        public string requestTime;
        public T data;
    }

    public class BitGetUnifiedInstrument
    {
        public string symbol;
        public string category;
        public string baseCoin;
        public string quoteCoin;
        public string buyLimitPriceRatio;
        public string sellLimitPriceRatio;
        public string feeRateUpRatio;
        public string makerFeeRate;
        public string takerFeeRate;
        public string openCostUpRatio;
        public string minOrderQty;
        public string maxOrderQty;
        public string pricePrecision;
        public string quantityPrecision;
        public string quotePrecision;
        public string priceMultiplier;
        public string quantityMultiplier;
        public string type;
        public string minOrderAmount;
        public string maxSymbolOrderNum;
        public string maxProductOrderNum;
        public string maxPositionNum;
        public string status;
        public string offTime;
        public string limitOpenTime;
        public string deliveryTime;
        public string deliveryStartTime;
        public string deliveryPeriod;
        public string launchTime;
        public string fundInterval;
        public string minLeverage;
        public string maxLeverage;
        public string maintainTime;
        public string maxMarketOrderQty;
        public string symbolType;
        public string isRwa;
        public string isIsolatedBaseBorrowable;
        public string isIsolatedQuotedBorrowable;
        public string warningRiskRatio;
        public string liquidationRiskRatio;
        public string maxCrossedLeverage;
        public string maxIsolatedLeverage;
        public string userMinBorrow;
        public string areaSymbol;
    }

    public class BGUFundingItem
    {
        public string symbol { get; set; }
        public string fundingRate { get; set; }
        public string fundingRateInterval { get; set; }
        public string nextUpdate { get; set; }
        public string minFundingRate { get; set; }
        public string maxFundingRate { get; set; }
    }

    public class BGUFundingHistory
    {
        public List<BGUFundingHistoryItem> resultList { get; set; }
    }

    public class BGUFundingHistoryItem
    {
        public string symbol { get; set; }
        public string fundingRate { get; set; }
        public string fundingRateTimestamp { get; set; }
    }

    public class UnifiedAccountInfo
    {
        public string uid { get; set; }
        public string accountMode { get; set; }
        public string assetMode { get; set; }
        public string accountLevel { get; set; }
        public string holdMode { get; set; }
        public string stpMode { get; set; }
        public Symbolconfiglist[] symbolConfigList { get; set; }
        public Coinconfiglist[] coinConfigList { get; set; }
    }

    public class Symbolconfiglist
    {
        public string category { get; set; }
        public string symbol { get; set; }
        public string marginMode { get; set; }
        public string leverage { get; set; }
    }

    public class Coinconfiglist
    {
        public string coin { get; set; }
        public string leverage { get; set; }
    }
 
    public class AccountData
    {
        public string accountEquity { get; set; }
        public string usdtEquity { get; set; }
        public string btcEquity { get; set; }
        public string unrealisedPnl { get; set; }
        public string usdtUnrealisedPnl { get; set; }
        public string btcUnrealizedPnl { get; set; }
        public string effEquity { get; set; }
        public string mmr { get; set; }
        public string imr { get; set; }
        public string mgnRatio { get; set; }
        public string positionMgnRatio { get; set; }
        public Asset[] assets { get; set; }
    }

    public class Asset
    {
        public string coin { get; set; }
        public string equity { get; set; }
        public string usdValue { get; set; }
        public string balance { get; set; }
        public string available { get; set; }
        public string debt { get; set; }
        public string locked { get; set; }
    }

    public class BGUPositions
    {
        public List<BGUPos> list { get; set; }
    }

    public class BGUPos
    {
        public string category { get; set; }
        public string symbol { get; set; }
        public string marginCoin { get; set; }
        public string holdMode { get; set; }
        public string posSide { get; set; }
        public string marginMode { get; set; }
        public string positionBalance { get; set; }
        public string available { get; set; }
        public string frozen { get; set; }
        public string total { get; set; }
        public string leverage { get; set; }
        public string curRealisedPnl { get; set; }
        public string avgPrice { get; set; }
        public string positionStatus { get; set; }
        public string unrealisedPnl { get; set; }
        public string liquidationPrice { get; set; }
        public string mmr { get; set; }
        public string profitRate { get; set; }
        public string markPrice { get; set; }
        public string breakEvenPrice { get; set; }
        public string totalFunding { get; set; }
        public string openFeeTotal { get; set; }
        public string closeFeeTotal { get; set; }
        public string createdTime { get; set; }
        public string updatedTime { get; set; }
    }

    public class BGUOrderResponse
    {
        public string clientOid { get; set; }
        public string orderId { get; set; }
    }

    public class BGUOrderInfo
    {
        public string orderId { get; set; }
        public string clientOid { get; set; }
        public string category { get; set; }
        public string symbol { get; set; }
        public string orderType { get; set; }
        public string side { get; set; }
        public string price { get; set; }
        public string qty { get; set; }
        public string amount { get; set; }
        public string cumExecQty { get; set; }
        public string cumExecValue { get; set; }
        public string avgPrice { get; set; }
        public string timeInForce { get; set; }
        public string orderStatus { get; set; }
        public string posSide { get; set; }
        public string holdMode { get; set; }
        public string delegateType { get; set; }
        public string reduceOnly { get; set; }
        public string marginMode { get; set; }
        public string stpMode { get; set; }
        public string takeProfit { get; set; }
        public string stopLoss { get; set; }
        public string tpTriggerBy { get; set; }
        public string slTriggerBy { get; set; }
        public string tpOrderType { get; set; }
        public string slOrderType { get; set; }
        public string tpLimitPrice { get; set; }
        public string slLimitPrice { get; set; }
        public Feedetail[] feeDetail { get; set; }
        public string createdTime { get; set; }
        public string updatedTime { get; set; }
        public string cancelReason { get; set; }
        public string execType { get; set; }
        public string tradeSide { get; set; }
    }

    public class BGUOrders
    {
        public BGUOrderInfo[] list { get; set; }
        public string cursor { get; set; }
    }

    public class BGUHistoryMyTrades
    {
        public BGUMyTrade[] list { get; set; }
        public string cursor { get; set; }
    }

    public class BGUMyTrade
    {
        public string execId { get; set; }
        public string execLinkId { get; set; }
        public string orderId { get; set; }
        public string clientOid { get; set; }
        public string category { get; set; }
        public string symbol { get; set; }
        public string orderType { get; set; }
        public string side { get; set; }
        public string execPrice { get; set; }
        public string execQty { get; set; }
        public string execValue { get; set; }
        public string tradeScope { get; set; }
        public string tradeSide { get; set; }
        public Feedetail[] feeDetail { get; set; }
        public string createdTime { get; set; }
        public string updatedTime { get; set; }
        public string execPnl { get; set; }
        public string isRPI { get; set; }
    }

    public class BGUOiData
    {
        public BGUOpenInterest[] list { get; set; }
        public string ts { get; set; }
    }

    public class BGUOpenInterest
    {
        public string symbol { get; set; }
        public string openInterest { get; set; }
    }
}
