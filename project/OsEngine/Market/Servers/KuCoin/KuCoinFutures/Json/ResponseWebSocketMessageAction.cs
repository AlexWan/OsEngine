using System.Collections.Generic;

namespace OsEngine.Market.Servers.KuCoin.KuCoinFutures.Json
{
    public class ResponseWebSocketBulletPrivate
    {
        public string endpoint;
        public string rotocol;
        public string encrypt;
        public string pingInterval;
        public string pingTimeout;
        public string token;
    }
    public class ResponseWebSocketMessageAction<T>
    {
        public string topic { get; set; }
        public string type { get; set; }
        public string subject { get; set; }
        public string userId { get; set; }
        public string channelType { get; set; }
        public T data { get; set; }
    }

    public class ResponseWebSocketMessageTrade
    {
        public string symbol { get; set; }
        public string sequence { get; set; }
        public string side { get; set; }
        public string size { get; set; }
        public string price { get; set; }
        public string takerOrderId { get; set; }
        public string makerOrderId { get; set; }
        public string tradeId { get; set; }
        public string ts { get; set; }
    }

    public class ResponseWebSocketDepthItem
    {
        public List<List<string>> asks;
        public List<List<string>> bids;

        public string timestamp;
    }

    public class ResponseWebSocketOrder
    {
        public string symbol { get; set; }
        public string orderType { get; set; }
        public string tradeType { get; set; }
        public string side { get; set; }
        public string canceledSize { get; set; }
        public string orderId { get; set; }
        public string liquidity { get; set; }
        public string marginMode { get; set; }
        public string type { get; set; }
        public string orderTime { get; set; }
        public string size { get; set; }
        public string filledSize { get; set; }
        public string price { get; set; }
        public string remainSize { get; set; }
        public string status { get; set; }
        public string ts { get; set; }
        public string matchPrice { get; set; }
        public string matchSize { get; set; }
        public string tradeId { get; set; }
        public string feeType { get; set; }
        public string clientOid { get; set; }
        public string positionSide { get; set; }
        public string tradeTypeAdditional { get; set; }
    }

    public class ResponseWebSocketPosition
    {
        public string symbol { get; set; }
        public string maintMarginReq { get; set; }
        public string riskLimit { get; set; }
        public string realLeverage { get; set; }
        public string crossMode { get; set; }
        public string delevPercentage { get; set; }
        public string openingTimestamp { get; set; }
        public string autoDeposit { get; set; }
        public string currentTimestamp { get; set; }
        public string currentQty { get; set; }
        public string currentCost { get; set; }
        public string currentComm { get; set; }
        public string unrealisedCost { get; set; }
        public string realisedCost { get; set; }
        public string isOpen { get; set; }
        public string markPrice { get; set; }
        public string markValue { get; set; }
        public string posCost { get; set; }
        public string posCross { get; set; }
        public string posInit { get; set; }
        public string posComm { get; set; }
        public string posLoss { get; set; }
        public string posMargin { get; set; }
        public string posFunding { get; set; }
        public string posMaint { get; set; }
        public string maintMargin { get; set; }
        public string avgEntryPrice { get; set; }
        public string liquidationPrice { get; set; }
        public string bankruptPrice { get; set; }
        public string settleCurrency { get; set; }
        public string changeReason { get; set; }
        public string riskLimitLevel { get; set; }
        public string realisedGrossCost { get; set; }
        public string realisedGrossPnl { get; set; }
        public string realisedPnl { get; set; }
        public string unrealisedPnl { get; set; }
        public string unrealisedPnlPcnt { get; set; }
        public string unrealisedRoePcnt { get; set; }
        public string leverage { get; set; }
        public string marginMode { get; set; }
        public string positionSide { get; set; }
    }

    public class ResponseWebSocketPortfolio
    {
        // https://www.kucoin.com/docs/websocket/futures-trading/private-channels/account-balance-events
        public string availableBalance; // 5923, //Current available amount
        public string holdBalance; // 2312, //Frozen amount = positionMargin + orderMargin + frozenFunds
        public string currency; // "USDT", //Currency
        public string timestamp; // 1553842862614
        public string isolatedOrderMargin;
        public string walletBalance;
    }

    public class FundingItem
    {
        public string granularity;
        public string fundingRate;
        public string timestamp;
    }
}
