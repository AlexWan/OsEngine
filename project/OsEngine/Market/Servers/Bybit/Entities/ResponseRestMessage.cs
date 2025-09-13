using System.Collections.Generic;

namespace OsEngine.Market.Servers.Bybit.Entities
{
    public class ResponseRestMessage<T>
    {
        public string retCode { get; set; }
        public string retMsg { get; set; }
        public T result { get; set; }
        public RetExtInfo retExtInfo { get; set; }
        public string time { get; set; }
    }

    public class ResponseRestMessageList<T>
    {
        public string retCode { get; set; }
        public string nextPageCursor { get; set; }
        public string retMsg { get; set; }
        public RetResalt<T> result { get; set; }
        public RetExtInfo retExtInfo { get; set; }
        public string time { get; set; }
    }

    public class TimeServer
    {
        public string timeSecond { get; set; }
        public string timeNano { get; set; }
    }

    public class APKeyInformation
    {
        public string id { get; set; }
        public string note { get; set; }
        public string apiKey { get; set; }
        public string readOnly { get; set; }
        public string secret { get; set; }
        public Permissions permissions { get; set; }
        public List<string> ips { get; set; }
        public string type { get; set; }
        public string deadlineDay { get; set; }
        public string expiredAt { get; set; }
        public string createdAt { get; set; }
        public string unified { get; set; }
        public string uta { get; set; }
        public string userID { get; set; }
        public string inviterID { get; set; }
        public string vipLevel { get; set; }
        public string mktMakerLevel { get; set; }
        public string affiliateID { get; set; }
        public string rsaPublicKey { get; set; }
        public string isMaster { get; set; }
        public string parentUid { get; set; }
        public string kycLevel { get; set; }
        public string kycRegion { get; set; }
    }

    public class Permissions
    {
        public List<string> ContractTrade { get; set; }
        public List<string> Spot { get; set; }
        public List<string> Wallet { get; set; }
        public List<string> Options { get; set; }
        public List<string> Derivatives { get; set; }
        public List<string> CopyTrading { get; set; }
        public List<string> BlockTrade { get; set; }
        public List<string> Exchange { get; set; }
        public List<string> NFT { get; set; }
        public List<string> Affiliate { get; set; }
        public List<string> Earn { get; set; }
    }

    public class LeverageFilter
    {
        public string minLeverage { get; set; }
        public string maxLeverage { get; set; }
        public string leverageStep { get; set; }
    }

    public class Symbols
    {
        public string symbol { get; set; }
        public string contractType { get; set; }
        public string status { get; set; }
        public string baseCoin { get; set; }
        public string quoteCoin { get; set; }
        public string launchTime { get; set; }
        public string deliveryTime { get; set; }
        public string deliveryFeeRate { get; set; }
        public string priceScale { get; set; }
        public LeverageFilter leverageFilter { get; set; }
        public PriceFilter priceFilter { get; set; }
        public LotSizeFilter lotSizeFilter { get; set; }
        public string unifiedMarginTrade { get; set; }
        public string fundingInterval { get; set; }
        public string settleCoin { get; set; }
        public string copyTrading { get; set; }
        public string upperFundingRate { get; set; }
        public string lowerFundingRate { get; set; }
        public string optionsType { get; set; }
    }

    public class LotSizeFilter
    {
        public string basePrecision { get; set; }
        public string quotePrecision { get; set; }
        public string maxOrderQty { get; set; }
        public string minOrderQty { get; set; }
        public string qtyStep { get; set; }
        public string postOnlyMaxOrderQty { get; set; }
        public string minOrderAmt { get; set; }
        public string maxOrderAmt { get; set; }
        public string maxMktOrderQty { get; set; }
        public string minNotionalValue { get; set; }
    }

    public class PriceFilter
    {
        public string minPrice { get; set; }
        public string maxPrice { get; set; }
        public string tickSize { get; set; }
    }

    public class ArraySymbols
    {
        public string category { get; set; }
        public List<Symbols> list { get; set; }
        public string nextPageCursor { get; set; }
    }

    public class RetExtInfo
    {
    }

    public class SendOrderResponse
    {
        public string orderId { get; set; }
        public string orderLinkId { get; set; }
    }

    public class RetResalt<T>
    {
        public string category { get; set; }
        public string nextPageCursor { get; set; }
        public List<T> list { get; set; }
    }

    public class RetTrade
    {
        public string execId { get; set; }
        public string symbol { get; set; }
        public string price { get; set; }
        public string size { get; set; }
        public string side { get; set; }
        public string time { get; set; }
        public string isBlockTrade { get; set; }
    }

    public class PositionOnBoardResult
    {
        public string symbol; // ": "ADAUSDT",
        public string leverage; // ": "10",
        public string autoAddMargin; // ": 0,
        public string avgPrice; // ": "0.3594",
        public string liqPrice; // ": "",
        public string riskLimitValue; // ": "200000",
        public string takeProfit; // ": "",
        public string positionValue; // ": "0.3594",
        public string isReduceOnly; // ": false,
        public string tpslMode; // ": "Full",
        public string riskId; // ": 116,
        public string trailingStop; // ": "0",
        public string unrealisedPnl; // ": "0.0001",
        public string markPrice; // ": "0.3595",
        public string adlRankIndicator; // ": 2,
        public string cumRealisedPnl; // ": "-0.0624684",
        public string positionMM; // ": "0.00301896",
        public string createdTime; // ": "1707043707699",
        public string positionIdx; // ": 0,
        public string positionIM; // ": "0.03626346",
        public string seq; // ": 162587161997,
        public string updatedTime; // ": "1730270283779",
        public string side; // ": "Buy",
        public string bustPrice; // ": "",
        public string positionBalance; // ": "0",
        public string leverageSysUpdatedTime; // ": "",
        public string curRealisedPnl; // ": "-0.0003594",
        public string size; // ": "1",
        public string positionStatus; // ": "Normal",
        public string mmrSysUpdatedTime; // ": "",
        public string stopLoss; // ": "",
        public string tradeMode; // ": 0,
        public string sessionAvgPrice; // ": ""
    }

    public class Result
    {
        public string Symbol { get; set; }
        public string Category { get; set; }
        public List<List<string>> List { get; set; }
    }

    public class AccountBalance
    {
        public string totalEquity { get; set; }
        public string accountIMRate { get; set; }
        public string totalMarginBalance { get; set; }
        public string totalInitialMargin { get; set; }
        public string accountType { get; set; }
        public string totalAvailableBalance { get; set; }
        public string accountMMRate { get; set; }
        public string totalPerpUPL { get; set; }
        public string totalWalletBalance { get; set; }
        public string accountLTV { get; set; }
        public string totalMaintenanceMargin { get; set; }
        public List<Coin> coin { get; set; }
    }

    public class Coin
    {
        public string availableToBorrow { get; set; }
        public string bonus { get; set; }
        public string accruedInterest { get; set; }
        public string availableToWithdraw { get; set; }
        public string totalOrderIM { get; set; }
        public string equity { get; set; }
        public string totalPositionMM { get; set; }
        public string usdValue { get; set; }
        public string spotHedgingQty { get; set; }
        public string unrealisedPnl { get; set; }
        public string collateralSwitch { get; set; }
        public string borrowAmount { get; set; }
        public string totalPositionIM { get; set; }
        public string walletBalance { get; set; }
        public string cumRealisedPnl { get; set; }
        public string locked { get; set; }
        public string marginCollateral { get; set; }
        public string coin { get; set; }
    }

    public class ResponseMessageOrders
    {
        public string orderId { get; set; }
        public string orderLinkId { get; set; }
        public string nextPageCursor { get; set; }
        public string blockTradeId { get; set; }
        public string symbol { get; set; }
        public string price { get; set; }
        public string qty { get; set; }
        public string side { get; set; }
        public string isLeverage { get; set; }
        public string positionIdx { get; set; }
        public string orderStatus { get; set; }
        public string cancelType { get; set; }
        public string rejectReason { get; set; }
        public string avgPrice { get; set; }
        public string leavesQty { get; set; }
        public string leavesValue { get; set; }
        public string cumExecQty { get; set; }
        public string cumExecValue { get; set; }
        public string cumExecFee { get; set; }
        public string timeInForce { get; set; }
        public string orderType { get; set; }
        public string stopOrderType { get; set; }
        public string orderIv { get; set; }
        public string triggerPrice { get; set; }
        public string takeProfit { get; set; }
        public string stopLoss { get; set; }
        public string tpTriggerBy { get; set; }
        public string slTriggerBy { get; set; }
        public string triggerDirection { get; set; }
        public string triggerBy { get; set; }
        public string lastPriceOnCreated { get; set; }
        public string reduceOnly { get; set; }
        public string closeOnTrigger { get; set; }
        public string smpType { get; set; }
        public string smpGroup { get; set; }
        public string smpOrderId { get; set; }
        public string tpslMode { get; set; }
        public string tpLimitPrice { get; set; }
        public string slLimitPrice { get; set; }
        public string placeType { get; set; }
        public string slippageToleranceType { get; set; }
        public string slippageTolerance { get; set; }
        public string createdTime { get; set; }
        public string updatedTime { get; set; }
    }

    public class ResponseMessageMyTrade
    {
        public string symbol { get; set; }
        public string orderType { get; set; }
        public string underlyingPrice { get; set; }
        public string orderLinkId { get; set; }
        public string side { get; set; }
        public string indexPrice { get; set; }
        public string orderId { get; set; }
        public string stopOrderType { get; set; }
        public string leavesQty { get; set; }
        public string execTime { get; set; }
        public string feeCurrency { get; set; }
        public string isMaker { get; set; }
        public string execFee { get; set; }
        public string feeRate { get; set; }
        public string execId { get; set; }
        public string tradeIv { get; set; }
        public string blockTradeId { get; set; }
        public string markPrice { get; set; }
        public string execPrice { get; set; }
        public string markIv { get; set; }
        public string orderQty { get; set; }
        public string orderPrice { get; set; }
        public string execValue { get; set; }
        public string execType { get; set; }
        public string execQty { get; set; }
        public string closedSize { get; set; }
        public string seq { get; set; }
    }
}
