/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using System.Text.Json.Serialization;

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
        [JsonPropertyName("responseType")]
        public string ResponseType { get; set; }

        [JsonPropertyName("subscribeType")]
        public string? SubscribeType { get; set; }

        [JsonPropertyName("ticker")]
        public string Ticker { get; set; }

        [JsonPropertyName("classCode")]
        public string ClassCode { get; set; }

        [JsonPropertyName("dateTime")]
        public string? DateTime { get; set; }

        [JsonPropertyName("side")]
        public string Side { get; set; }

        [JsonPropertyName("volume")]
        public string? Volume { get; set; }

        [JsonPropertyName("price")]
        public string Price { get; set; }

        [JsonPropertyName("quantity")]
        public string Quantity { get; set; }

        [JsonPropertyName("depth")]
        public string Depth { get; set; }

        [JsonPropertyName("bidVolume")]
        public string BidVolume { get; set; }

        [JsonPropertyName("askVolume")]
        public string AskVolume { get; set; }

        [JsonPropertyName("bids")]
        public List<OrderBookEntry> Bids { get; set; }

        [JsonPropertyName("asks")]
        public List<OrderBookEntry> Asks { get; set; }

        [JsonPropertyName("errors")]
        public List<Error> Errors { get; set; }
    }

    public class OrderBookEntry
    {
        [JsonPropertyName("price")]
        public string Price { get; set; }

        [JsonPropertyName("quantity")]
        public string Quantity { get; set; }
    }

    public class Error
    {
        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("field")]
        public string Field { get; set; }
    }

    public class BcsOrdersResponse
    {
        [JsonPropertyName("originalClientOrderId")]
        public string OriginalClientOrderId { get; set; }

        [JsonPropertyName("clientOrderId")]
        public string ClientOrderId { get; set; }

        [JsonPropertyName("data")]
        public Data Data { get; set; }
    }

    public class Data
    {
        [JsonPropertyName("messageType")]
        public string MessageType { get; set; }

        [JsonPropertyName("orderStatus")]
        public string OrderStatus { get; set; }

        [JsonPropertyName("executionType")]
        public string ExecutionType { get; set; }

        [JsonPropertyName("orderQuantity")]
        public string OrderQuantity { get; set; }

        [JsonPropertyName("executedQuantity")]
        public string ExecutedQuantity { get; set; }

        [JsonPropertyName("lastQuantity")]
        public string LastQuantity { get; set; }

        [JsonPropertyName("remainedQuantity")]
        public string RemainedQuantity { get; set; }

        [JsonPropertyName("ticker")]
        public string Ticker { get; set; }

        [JsonPropertyName("classCode")]
        public string ClassCode { get; set; }

        [JsonPropertyName("side")]
        public string Side { get; set; }

        [JsonPropertyName("orderType")]
        public string OrderType { get; set; }

        [JsonPropertyName("averagePrice")]
        public string AveragePrice { get; set; }

        [JsonPropertyName("orderId")]
        public string OrderId { get; set; }

        [JsonPropertyName("executionId")]
        public string ExecutionId { get; set; }

        [JsonPropertyName("price")]
        public string Price { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        [JsonPropertyName("clientCode")]
        public string ClientCode { get; set; }

        [JsonPropertyName("transactionTime")]
        public string TransactionTime { get; set; }

        [JsonPropertyName("tradeDate")]
        public string TradeDate { get; set; }

        [JsonPropertyName("orderNumber")]
        public string OrderNumber { get; set; }

        [JsonPropertyName("accruedCoupon")]
        public string AccruedCoupon { get; set; }

        [JsonPropertyName("executionValue")]
        public string ExecutionValue { get; set; }

        [JsonPropertyName("commission")]
        public string Commission { get; set; }

        [JsonPropertyName("securityExchange")]
        public string SecurityExchange { get; set; }

        [JsonPropertyName("rejectReason")]
        public string RejectReason { get; set; }
    }


    public class WarningSocketMessage
    {
        public Displayoptions displayOptions { get; set; }
        public long timestamp { get; set; }
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
        public long timestamp { get; set; }
        public string traceId { get; set; }
        public string type { get; set; }
    }
}
