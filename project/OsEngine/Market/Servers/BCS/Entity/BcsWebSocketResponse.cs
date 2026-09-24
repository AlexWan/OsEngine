/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BCS.Entity
{
    public class BcsPortfolio
    {
        public string type { get; set; }
        public string subAccountId { get; set; }
        public string agreementId { get; set; }
        public string account { get; set; }
        public string exchange { get; set; }
        public string ticker { get; set; }
        public string displayName { get; set; }
        public string baseAssetTicker { get; set; }
        public string currency { get; set; }
        public string upperType { get; set; }
        public string instrumentType { get; set; }
        public string term { get; set; }
        public string quantity { get; set; }
        public string locked { get; set; }
        public string balancePrice { get; set; }
        public string currentPrice { get; set; }
        public string balanceValue { get; set; }
        public string balanceValueRub { get; set; }
        public string balanceValueUsd { get; set; }
        public string balanceValueEur { get; set; }
        public string currentValue { get; set; }
        public string currentValueRub { get; set; }
        public string currentValueUsd { get; set; }
        public string currentValueEur { get; set; }
        public string unrealizedPL { get; set; }
        public string unrealizedPercentPL { get; set; }
        public string dailyPL { get; set; }
        public string dailyPercentPL { get; set; }
        public string portfolioShare { get; set; }
        public string scale { get; set; }
        public string minimumStep { get; set; }
        public string board { get; set; }
        public string priceUnit { get; set; }
        public string faceValue { get; set; }
        public string accruedIncome { get; set; }
        public string logoLink { get; set; }
        public string isBlocked { get; set; }
        public string isBlockedTradeAccount { get; set; }
        public string lockedForFutures { get; set; }
        public string ratioQuantity { get; set; }
        public string expireDate { get; set; }
    }

    public class PublicMarketDataResponse
    {
        public string responseType { get; set; }
        public string subscribeType { get; set; }
        public string ticker { get; set; }
        public string classCode { get; set; }
        public string dateTime { get; set; }
        public string side { get; set; }
        public string volume { get; set; }
        public string price { get; set; }
        public string quantity { get; set; }
        public string depth { get; set; }
        public string bidVolume { get; set; }
        public string askVolume { get; set; }
        public List<OrderBookEntry> bids { get; set; }
        public List<OrderBookEntry> asks { get; set; }
        public List<Error> errors { get; set; }
    }

    public class OrderBookEntry
    {
        public string price { get; set; }
        public string quantity { get; set; }
    }

    public class Error
    {
        public string message { get; set; }
        public string code { get; set; }
        public string type { get; set; }
        public string field { get; set; }
    }

    public class BcsOrdersResponse
    {
        public string originalClientOrderId { get; set; }
        public string clientOrderId { get; set; }
        public Data data { get; set; }
    }

    public class Data
    {
        public string messageType { get; set; }
        public string orderStatus { get; set; }
        public string executionType { get; set; }
        public string orderQuantity { get; set; }
        public string executedQuantity { get; set; }
        public string lastQuantity { get; set; }
        public string remainedQuantity { get; set; }
        public string ticker { get; set; }
        public string classCode { get; set; }
        public string side { get; set; }
        public string orderType { get; set; }
        public string averagePrice { get; set; }
        public string orderId { get; set; }
        public string executionId { get; set; }
        public string price { get; set; }
        public string currency { get; set; }
        public string clientCode { get; set; }
        public string transactionTime { get; set; }
        public string tradeDate { get; set; }
        public string orderNumber { get; set; }
        public string accruedCoupon { get; set; }
        public string executionValue { get; set; }
        public string commission { get; set; }
        public string securityExchange { get; set; }
        public string rejectReason { get; set; }
    }

    public class WarningSocketMessage
    {
        public Displayoptions displayOptions { get; set; }
        public string timestamp { get; set; }
        public string traceId { get; set; }
        public string type { get; set; }
    }

    public class Displayoptions
    {
        public string text { get; set; }
    }

    public class ErrorSubscribeSocket
    {
        public Error[] errors { get; set; }
        public string timestamp { get; set; }
        public string traceId { get; set; }
        public string type { get; set; }
    }
}
