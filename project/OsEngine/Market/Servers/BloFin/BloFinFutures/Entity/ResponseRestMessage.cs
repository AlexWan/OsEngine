using System.Collections.Generic;


namespace OsEngine.Market.Servers.BloFin.Entity
{
    public class ResponseRestMessage<T>
    {
        public string code { get; set; }
        public string msg { get; set; }
        public T data { get; set; }
    }

    public class APIKeyInfoData
    {
        public string uid { get; set; }
        public string apiName { get; set; }
        public string apiKey { get; set; }
        public string readOnly { get; set; }
        public List<string> ips { get; set; }
        public string type { get; set; }
        public string expireTime { get; set; }
        public string createTime { get; set; }
        public string referralCode { get; set; }
        public string parentUid { get; set; }
    }

    public class RestMessageInstruments
    {
        public string instId { get; set; }
        public string baseCurrency { get; set; }
        public string quoteCurrency { get; set; }
        public string contractValue { get; set; }
        public string listTime { get; set; }
        public string expireTime { get; set; }
        public string maxLeverage { get; set; }
        public string minSize { get; set; }
        public string lotSize { get; set; }
        public string tickSize { get; set; }
        public string instType { get; set; }
        public string contractType { get; set; }
        public string maxLimitSize { get; set; }
        public string maxMarketSize { get; set; }
        public string state { get; set; }
    }

    public class RestMessageBalance
    {
        public string ts { get; set; }
        public string totalEquity { get; set; }
        public string isolatedEquity { get; set; }
        public List<Detail> details { get; set; }
    }

    public class Detail
    {
        public string currency { get; set; }
        public string equity { get; set; }
        public string balance { get; set; }
        public string ts { get; set; }
        public string isolatedEquity { get; set; }
        public string available { get; set; }
        public string availableEquity { get; set; }
        public string frozen { get; set; }
        public string orderFrozen { get; set; }
        public string equityUsd { get; set; }
        public string isolatedUnrealizedPnl { get; set; }
        public string bonus { get; set; }
    }

    public class RestMessagePosition
    {
        public string positionId { get; set; }
        public string instId { get; set; }
        public string instType { get; set; }
        public string marginMode { get; set; }
        public string positionSide { get; set; }
        public string adl { get; set; }
        public string positions { get; set; }
        public string availablePositions { get; set; }
        public string averagePrice { get; set; }
        public string margin { get; set; }
        public string markPrice { get; set; }
        public string marginRatio { get; set; }
        public string liquidationPrice { get; set; }
        public string unrealizedPnl { get; set; }
        public string unrealizedPnlRatio { get; set; }
        public string maintenanceMargin { get; set; }
        public string createTime { get; set; }
        public string updateTime { get; set; }
        public string leverage { get; set; }
    }

    public class RestMessageCandle
    {
        public List<List<string>> data { get; set; }
    }

    public class RestMessagePositionMode
    {
        public string positionMode { get; set; }
    }

    public class RestMessageMarginMode
    {
        public string marginMode { get; set; }
    }

    public class RestMessageSendOrder
    {
        public string orderId { get; set; }
        public string clientOrderId { get; set; }
        public string msg { get; set; }
        public string code { get; set; }
    }

    public class RestMessageOrder
    {
        public string orderId { get; set; }
        public string clientOrderId { get; set; }
        public string instId { get; set; }
        public string marginMode { get; set; }
        public string positionSide { get; set; }
        public string side { get; set; }
        public string orderType { get; set; }
        public string price { get; set; }
        public string size { get; set; }
        public string reduceOnly { get; set; }
        public string leverage { get; set; }
        public string state { get; set; }
        public string filledSize { get; set; }
        public string filled_amount { get; set; }
        public string averagePrice { get; set; }
        public string fee { get; set; }
        public string pnl { get; set; }
        public string createTime { get; set; }
        public string updateTime { get; set; }
        public string orderCategory { get; set; }
        public string tpTriggerPrice { get; set; }
        public string slTriggerPrice { get; set; }
        public string slOrderPrice { get; set; }
        public string tpOrderPrice { get; set; }
        public string cancelSource { get; set; }
        public string cancelSourceReason { get; set; }
        public string algoClientOrderId { get; set; }
        public string algoId { get; set; }
        public string brokerId { get; set; }
    }

    public class RestMessageTrade
    {
        public string instId { get; set; }
        public string tradeId { get; set; }
        public string orderId { get; set; }
        public string fillPrice { get; set; }
        public string fillSize { get; set; }
        public string fillPnl { get; set; }
        public string positionSide { get; set; }
        public string side { get; set; }
        public decimal fee { get; set; }
        public string ts { get; set; }
        public string brokerId { get; set; }
    }
}
