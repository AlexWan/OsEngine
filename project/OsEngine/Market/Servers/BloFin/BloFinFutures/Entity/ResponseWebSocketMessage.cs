using System.Collections.Generic;


namespace OsEngine.Market.Servers.BloFin.Entity
{
    public class ResponseWebSocketMessage<T>
    {
        public string action { get; set; }
        public ResponseWebSocketMessageArg arg { get; set; }
        public T data { get; set; }
        public string Event { get; set; }
        public string code { get; set; }
        public string msg { get; set; }
    }

    public class ResponseWebSocketMessageSubscribe
    {

    }

    public class ResponseWebSocketMessageArg
    {
        public string channel { get; set; }
        public string instId { get; set; }
    }

    public class ResponseWebSocketTrades
    {
        public string instId { get; set; }
        public string tradeId { get; set; }
        public string price { get; set; }
        public string size { get; set; }
        public string side { get; set; }
        public string ts { get; set; }
    }

    public class ResponseWebSocketDepth
    {
        public List<List<string>> asks { get; set; }
        public List<List<string>> bids { get; set; }
        public string ts { get; set; }
        public string prevSeqId { get; set; }
        public string seqId { get; set; }
    }

    public class ResponseWebSocketOrder
    {
        public string instType { get; set; }
        public string instId { get; set; }
        public string orderId { get; set; }
        public string clientOrderId { get; set; }
        public string price { get; set; }
        public string size { get; set; }
        public string orderType { get; set; }
        public string side { get; set; }
        public string positionSide { get; set; }
        public string marginMode { get; set; }
        public string filledSize { get; set; }
        public string filledAmount { get; set; }
        public string averagePrice { get; set; }
        public string state { get; set; }
        public string leverage { get; set; }
        public string tpTriggerPrice { get; set; }
        public string tpTriggerPriceType { get; set; }
        public string tpOrderPrice { get; set; }
        public string slTriggerPrice { get; set; }
        public string slTriggerPriceType { get; set; }
        public string slOrderPrice { get; set; }
        public string fee { get; set; }
        public string pnl { get; set; }
        public string cancelSource { get; set; }
        public string orderCategory { get; set; }
        public string createTime { get; set; }
        public string updateTime { get; set; }
        public string reduceOnly { get; set; }
        public string brokerId { get; set; }
    }

    public class ResponseWebSocketPosition
    {
        public string instType { get; set; }
        public string instId { get; set; }
        public string marginMode { get; set; }
        public string positionId { get; set; }
        public string positionSide { get; set; }
        public string positions { get; set; }
        public string availablePositions { get; set; }
        public string averagePrice { get; set; }
        public string unrealizedPnl { get; set; }
        public string unrealizedPnlRatio { get; set; }
        public string leverage { get; set; }
        public string liquidationPrice { get; set; }
        public string markPrice { get; set; }
        public string initialMargin { get; set; }
        public string margin { get; set; }
        public string marginRatio { get; set; }
        public string maintenanceMargin { get; set; }
        public string adl { get; set; }
        public string createTime { get; set; }
        public string updateTime { get; set; }
    }

    public class ResponseWebSocketAccount
    {
        public string ts { get; set; }
        public string totalEquity { get; set; }
        public string isolatedEquity { get; set; }
        public List<ResponseWebSockeDetail> details { get; set; }
    }

    public class ResponseWebSockeDetail
    {
        public string currency { get; set; }
        public string equity { get; set; }
        public string balance { get; set; }
        public string ts { get; set; }
        public string isolatedEquity { get; set; }
        public string equityUsd { get; set; }
        public string availableEquity { get; set; }
        public string available { get; set; }
        public string frozen { get; set; }
        public string orderFrozen { get; set; }
        public string unrealizedPnl { get; set; }
        public string isolatedUnrealizedPnl { get; set; }
    }
}
