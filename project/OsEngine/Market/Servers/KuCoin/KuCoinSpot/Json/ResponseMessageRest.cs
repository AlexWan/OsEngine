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
    

    public class ResponseMyTrade
    {
        public string symbol; // "BTC-USDT",
        public string tradeId; // "5c35c02709e4f67d5266954e",
        public string orderId; // "5c35c02703aa673ceec2a168",
        public string counterOrderId; // "5c1ab46003aa676e487fa8e3",
        public string side; // "buy",
        public string liquidity; // "taker",
        public string forceTaker; // true,
        public string price; // "0.083",
        public string size; // "0.8424304",
        public string funds; // "0.0699217232",
        public string fee; // "0",
        public string feeRate; // "0",
        public string feeCurrency; // "USDT",
        public string stop; // "",
        public string type; // "limit",
        public string createdAt; // 1547026472000,
        public string tradeType; // "TRADE"
    }

    public class ResponseMyTrades
    {
        public string currentPage;
        public string pageSize;
        public string totalNum;
        public string totalPage;

        public List<ResponseMyTrade> items;
    }
}
