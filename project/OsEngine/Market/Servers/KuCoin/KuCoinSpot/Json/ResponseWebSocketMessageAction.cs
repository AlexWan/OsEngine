using System.Collections.Generic;

namespace OsEngine.Market.Servers.KuCoin.KuCoinSpot.Json
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
        public string makerOrderId { get; set; }
        public string price { get; set; }
        public string sequence { get; set; }
        public string side { get; set; }
        public string size { get; set; }
        public string symbol { get; set; }
        public string takerOrderId { get; set; }
        public string time { get; set; }
        public string tradeId { get; set; }
        public string type { get; set; }
    }

    public class ResponseWebSocketDepthItem
    {
        public List<List<string>> asks;
        public List<List<string>> bids;

        public string timestamp;
    }

    public class ResponseWebSocketOrder
    {
        public string clientOid { get; set; }
        public string orderId { get; set; }
        public string orderTime { get; set; }
        public string orderType { get; set; }
        public string originSize { get; set; }
        public string side { get; set; }
        public string status { get; set; }
        public string symbol { get; set; }
        public string ts { get; set; }
        public string type { get; set; }
        public string canceledSize { get; set; }
        public string filledSize { get; set; }
        public string price { get; set; }
        public string remainSize { get; set; }
        public string size { get; set; }
        public string oldSize { get; set; }
        public string feeType { get; set; }
        public string liquidity { get; set; }
        public string matchPrice { get; set; }
        public string matchSize { get; set; }
        public string tradeId { get; set; }
        public string remainFunds { get; set; }
    }

    public class RelationContext
    {
        public string symbol; //"BTC-USDT",
        public string tradeId; //"5e6a5dca9e16882a7d83b7a4", // the trade Id when order is executed
        public string orderId; //"5ea10479415e2f0009949d54"
    }

    public class ResponseWebSocketPortfolio
    {
        // https://www.kucoin.com/docs/websocket/spot-trading/private-channels/account-balance-change
        public string total; //"88", // total balance
        public string available; //"88", // available balance
        public string availableChange; //"88", // the change of available balance
        public string currency; //"KCS", // currency
        public string hold; //"0", // hold amount
        public string holdChange; //"0", // the change of hold balance
        public string relationEvent; //"trade.setted", //relation event
        public string relationEventId; //"5c21e80303aa677bd09d7dff", // relation event id

        public RelationContext relationContext;

        public string time; //"1545743136994" // timestamp
    }

    public class TickerData
    {
        public string sequence { get; set; }
        public TickerItem data { get; set; }
    }

    public class TickerItem
    {
        public string askSize { get; set; }
        public string averagePrice { get; set; }
        public string baseCurrency { get; set; }
        public string bidSize { get; set; }
        public string board { get; set; }
        public string buy { get; set; }
        public string changePrice { get; set; }
        public string changeRate { get; set; }
        public string close { get; set; }
        public string datetime { get; set; }
        public string high { get; set; }
        public string lastTradedPrice { get; set; }
        public string low { get; set; }
        public string makerCoefficient { get; set; }
        public string makerFeeRate { get; set; }
        public string marginTrade { get; set; }
        public string mark { get; set; }
        public string market { get; set; }
        public MarketChange marketChange1h { get; set; }
        public MarketChange marketChange24h { get; set; }
        public MarketChange marketChange4h { get; set; }
        public List<string> markets { get; set; }
        public string open { get; set; }
        public string quoteCurrency { get; set; }
        public string sell { get; set; }
        public List<string> siteTypes { get; set; }
        public string sort { get; set; }
        public string symbol { get; set; }
        public string symbolCode { get; set; }
        public string takerCoefficient { get; set; }
        public string takerFeeRate { get; set; }
        public string trading { get; set; }
        public string vol { get; set; }
        public string volValue { get; set; }
    }

    public class MarketChange
    {
        public string changePrice { get; set; }
        public string changeRate { get; set; }
        public string high { get; set; }
        public string low { get; set; }
        public string open { get; set; }
        public string vol { get; set; }
        public string volValue { get; set; }
    }
}
