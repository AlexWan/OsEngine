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
        public string type;
        public string topic;
        public string subject;
        public T data;
    }

    public class ResponseWebSocketMessageTrade
    {
        public string sequence;
        public string type;
        public string symbol;
        public string size;
        public string side;
        public string price;
        public string takerOrderId;
        public string makerOrderId;
        public string time;
    }

    public class ResponseWebSocketDepthItem
    {
        public List<List<string>> asks;
        public List<List<string>> bids;

        public string timestamp;
    }

    public class ResponseWebSocketOrder
    {
        // https://www.kucoin.com/docs/websocket/spot-trading/private-channels/private-order-change-v2
        public string symbol; //  KCS-USDT ,
        public string orderType; //  limit ,
        public string side; //  sell ,
        public string orderId; //  5efab07953bdea00089965fa ,
        public string liquidity; //  taker ,
        public string type; //  match ,
        public string orderTime; // 1593487482038606180,
        public string size; //  0.1 ,
        public string filledSize; //  0.1 ,
        public string price; //  0.938 ,
        public string matchPrice; //  0.96738 ,
        public string matchSize; //  0.1 ,
        public string tradeId; //  5efab07a4ee4c7000a82d6d9 ,
        public string clientOid; //  1593487481000313 ,
        public string remainSize; //  0 ,
        public string status; //  match ,
        public string canceledSize; //  0.1 , // Cumulative number of cancellations
        public string canceledFunds; //  0.1 , // Market order accumulative cancellation funds
        public string originSize; //  0.1 , // original quantity
        public string originFunds; //  0.1 , // Market order original funds
        public string ts; // 1593487482038606180 nanoseconds
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
        public string lastSize { get; set; }
        public string lastTradedPrice { get; set; }
        public string low { get; set; }
        public string makerCoefficient { get; set; }
        public string makerFeeRate { get; set; }
        public string marginTrade { get; set; }
        public string mark { get; set; } // 0,1,2
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
