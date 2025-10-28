using System.Collections.Generic;


namespace OsEngine.Market.Servers.KuCoin.KuCoinSpot.Json
{
    public class ResponseMessageRest<T>
    {
        public string code;
        public string msg;
        public T data;
    }

    public class ResponsePrivateWebSocketConnection
    {
        public string code;

        public class InstanceServer
        {
            public string endpoint;
            public string encrypt;
            public string protocol;
            public string pingInterval;
            public string pingTimeout;
        }

        public class WSData
        {
            public string token;
            public List<InstanceServer> instanceServers;
        }

        public WSData data;
    }

    public class ResponsePlaceOrder
    {
        public string orderId;
    }

    public class ResponseAllOrders
    {
        public string currentPage;
        public string pageSize;
        public string totalNum;
        public string totalPage;

        public List<ResponseOrder> items;
    }

    public class ResponseOrder
    {
        public string id { get; set; }
        public string symbol { get; set; }
        public string opType { get; set; }
        public string type { get; set; }
        public string side { get; set; }
        public string price { get; set; }
        public string size { get; set; }
        public string funds { get; set; }
        public string dealSize { get; set; }
        public string dealFunds { get; set; }
        public string fee { get; set; }
        public string feeCurrency { get; set; }
        public string stp { get; set; }
        public string timeInForce { get; set; }
        public string postOnly { get; set; }
        public string hidden { get; set; }
        public string iceberg { get; set; }
        public string visibleSize { get; set; }
        public string cancelAfter { get; set; }
        public string channel { get; set; }
        public string clientOid { get; set; }
        public string remark { get; set; }
        public string tags { get; set; }
        public string cancelExist { get; set; }
        public string createdAt { get; set; }
        public string lastUpdatedAt { get; set; }
        public string tradeType { get; set; }
        public string inOrderBook { get; set; }
        public string cancelledSize { get; set; }
        public string cancelledFunds { get; set; }
        public string remainSize { get; set; }
        public string remainFunds { get; set; }
        public string tax { get; set; }
        public string cancelReason { get; set; }
        public string active { get; set; }
    }

    public class Ticker
    {
        public string sequence;
        public string price;
        public string size;
        public string bestAsk;
        public string bestAskSize;
        public string bestBid;
        public string bestBidSize;
        public string time;
    }

    public class ResponseAsset
    {
        public string id;
        public string currency;
        public string balance;
        public string available;
        public string frozen;
        public string holds;
    }

    public class ResponseSymbol
    {
        public string symbol;
        public string name;
        public string baseCurrency;
        public string quoteCurrency;
        public string feeCurrency;
        public string market;
        public string baseMinSize;
        public string baseMaxSize;
        public string quoteMaxSize;
        public string baseIncrement;
        public string quoteIncrement;
        public string priceIncrement;
        public string priceLimitRate;
        public string minFunds;
        public string isMarginEnabled;
        public string enableTrading;
    }

    public class ActiveOrderSymbols
    {
        public List<string> symbols { get; set; }
    }

    public class ResponseMyTrade
    {
        public string id { get; set; }
        public string orderId { get; set; }
        public string counterOrderId { get; set; }
        public string tradeId { get; set; }
        public string symbol { get; set; }
        public string side { get; set; }
        public string liquidity { get; set; }
        public string type { get; set; }
        public bool forceTaker { get; set; }
        public string price { get; set; }
        public string size { get; set; }
        public string funds { get; set; }
        public string fee { get; set; }
        public string feeRate { get; set; }
        public string feeCurrency { get; set; }
        public string stop { get; set; }
        public string tradeType { get; set; }
        public string taxRate { get; set; }
        public string tax { get; set; }
        public string createdAt { get; set; }
    }

    public class ResponseMyTrades
    {
        public List<ResponseMyTrade> items { get; set; }
        public string lastId { get; set; }
    }
}
