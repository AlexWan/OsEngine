using System.Collections.Generic;

namespace OsEngine.Market.Servers.Woo.Entity
{
    public class ResponseMessageRest<T>
    {
        public string success { get; set; }
        public string timestamp { get; set; }
        public T data { get; set; }
    }

    public class ResponseListenKey
    {
        public string authKey { get; set; }
        public string expiredTime { get; set; }
    }

    public class ResponseSystemStatus
    {
        public string status { get; set; }
        public string msg { get; set; }
        public string estimatedEndTime { get; set; }
    }

    public class ResponseSecurities
    {
        public List<RowSymbols> rows { get; set; }
    }

    public class RowSymbols
    {
        public string symbol { get; set; }
        public string status { get; set; }
        public string baseAsset { get; set; }
        public string baseAssetMultiplier { get; set; }
        public string quoteAsset { get; set; }
        public string quoteMin { get; set; }
        public string quoteMax { get; set; }
        public string quoteTick { get; set; }
        public string baseMin { get; set; }
        public string baseMax { get; set; }
        public string baseTick { get; set; }
        public string minNotional { get; set; }
        public string bidCapRatio { get; set; }
        public string bidFloorRatio { get; set; }
        public string askCapRatio { get; set; }
        public string askFloorRatio { get; set; }
        public string fundingIntervalHours { get; set; }
        public string fundingCap { get; set; }
        public string fundingFloor { get; set; }
        public string orderMode { get; set; }
        public string baseIMR { get; set; }
        public string baseMMR { get; set; }
        public string isAllowedRpi { get; set; }
    }

    public class ResponseCommonPortfolio
    {
        public string applicationId { get; set; }
        public string account { get; set; }
        public string alias { get; set; }
        public string otpauth { get; set; }
        public string accountMode { get; set; }
        public string positionMode { get; set; }
        public string leverage { get; set; }
        public string marginRatio { get; set; }
        public string openMarginRatio { get; set; }
        public string initialMarginRatio { get; set; }
        public string maintenanceMarginRatio { get; set; }
        public string totalCollateral { get; set; }
        public string freeCollateral { get; set; }
        public string totalAccountValue { get; set; }
        public string totalTradingValue { get; set; }
        public string totalVaultValue { get; set; }
        public string totalStakingValue { get; set; }
        public string totalEarnValue { get; set; }
        public string totalLaunchpadValue { get; set; }
        public string referrerID { get; set; }
        public string accountType { get; set; }
    }

    public class ResponsePortfolios
    {
        public List<Holding> holding { get; set; }
    }

    public class Holding
    {
        public string token { get; set; }
        public string holding { get; set; }
        public string frozen { get; set; }
        public string staked { get; set; }
        public string unbonding { get; set; }
        public string vault { get; set; }
        public string interest { get; set; }
        public string earn { get; set; }
        public string pendingShortQty { get; set; }
        public string pendingLongQty { get; set; }
        public string availableBalance { get; set; }
        public string averageOpenPrice { get; set; }
        public string markPrice { get; set; }
        public string pnl24H { get; set; }
        public string fee24H { get; set; }
        public string timestamp { get; set; }
    }

    public class MarketDepthData
    {
        public List<OrderLevel> asks { get; set; }
        public List<OrderLevel> bids { get; set; }
    }

    public class OrderLevel
    {
        public string price { get; set; }
        public string quantity { get; set; }
    }

    public class ResponseFuturesPositions
    {
        public List<FuturesPosition> positions { get; set; }
    }

    public class FuturesPosition
    {
        public string symbol { get; set; }
        public string holding { get; set; }
        public string positionSide { get; set; }
        public string pendingLongQty { get; set; }
        public string pendingShortQty { get; set; }
        public string settlePrice { get; set; }
        public string averageOpenPrice { get; set; }
        public string pnl24H { get; set; }
        public string fee24H { get; set; }
        public string markPrice { get; set; }
        public string estLiqPrice { get; set; }
        public string adlQuantile { get; set; }
        public string timestamp { get; set; }
    }

    public class ResponseCandles
    {
        public List<RowCandles> rows { get; set; }
    }

    public class RowCandles
    {
        public string symbol { get; set; }
        public string open { get; set; }
        public string close { get; set; }
        public string high { get; set; }
        public string low { get; set; }
        public string volume { get; set; }
        public string amount { get; set; }
        public string type { get; set; }
        public string startTimestamp { get; set; }
        public string endTimestamp { get; set; }
    }

    public class OrderData
    {
        public string orderId { get; set; }
        public string type { get; set; }
        public string price { get; set; }
        public string quantity { get; set; }
        public string amount { get; set; }
        public string clientOrderId { get; set; }
        public string bidAskLevel { get; set; }
    }

    public class OpenOrdersData
    {
        public MetaPages meta { get; set; }
        public List<RowOpenOrders> rows { get; set; }
    }

    public class MetaPages
    {
        public string total { get; set; }
        public string recordsPerPage { get; set; }
        public string currentPage { get; set; }
    }

    public class RowOpenOrders
    {
        public string symbol { get; set; }
        public string status { get; set; }
        public string side { get; set; }
        public string positionSide { get; set; }
        public string createdTime { get; set; }
        public string orderId { get; set; }
        public string orderTag { get; set; }
        public string price { get; set; }
        public string type { get; set; }
        public string quantity { get; set; }
        public string amount { get; set; }
        public string visible { get; set; }
        public string executed { get; set; }
        public string totalFee { get; set; }
        public string feeAsset { get; set; }
        public string totalRebate { get; set; }
        public string rebateCurrency { get; set; }
        public string clientOrderId { get; set; }
        public string reduceOnly { get; set; }
        public string realizedPnl { get; set; }
        public string averageExecutedPrice { get; set; }
    }

    public class MyTradeData
    {
        public List<RowMyTrade> rows { get; set; }
        public MetaPages meta { get; set; }
    }

    public class RowMyTrade
    {
        public string id { get; set; }
        public string symbol { get; set; }
        public string orderId { get; set; }
        public string orderTag { get; set; }
        public string executedPrice { get; set; }
        public string executedQuantity { get; set; }
        public string isMaker { get; set; }
        public string isMatchRpi { get; set; }
        public string side { get; set; }
        public string fee { get; set; }
        public string feeAsset { get; set; }
        public string realizedPnl { get; set; }
        public string executedTimestamp { get; set; }
    }

    public class FundingHistoryData
    {
        public MetaPages meta { get; set; }
        public List<RowFunding> rows { get; set; }
    }

    public class RowFunding
    {
        public string symbol { get; set; }
        public string fundingRate { get; set; }
        public string fundingRateTimestamp { get; set; }
        public string nextFundingTime { get; set; }
        public string markPrice { get; set; }
    }

    public class EstimatedFundingRateData
    {
        public List<EstimatedFundingRateRow> rows { get; set; }
    }

    public class EstimatedFundingRateRow
    {
        public string symbol { get; set; }
        public string estFundingRate { get; set; }
        public string estFundingRateTimestamp { get; set; }
        public string lastFundingRate { get; set; }
        public string lastFundingRateTimestamp { get; set; }
        public string nextFundingTime { get; set; }
        public string lastFundingIntervalHours { get; set; }
        public string estFundingIntervalHours { get; set; }
    }
}
