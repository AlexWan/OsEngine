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
        public string id { get; set; } // Order ID
        public string symbol { get; set; } // Symbol of the contract
        public string opType { get; set; } // Operation type: DEAL
        public string type { get; set; } // order type
        public string side { get; set; } // transaction direction,include buy and sell
        public string price { get; set; } // Order price
        public string size { get; set; } // Order quantity
        public string funds { get; set; } // order funds
        public string dealFunds { get; set; } // executed size of funds
        public string dealSize { get; set; } // Executed quantity
        public string fee { get; set; } // fee
        public string feeCurrency { get; set; } // charge fee currency
        public string stp { get; set; } // self trade prevention,include CN,CO,DC,CB
        public string stop { get; set; } // stop type, include entry and loss
        public string stopTriggered { get; set; } // stop order is triggered or not
        public string stopPrice { get; set; } // stop price
        public string timeInForce { get; set; } // time InForce,include GTC,GTT,IOC,FOK
        public string postOnly { get; set; } // postOnly
        public string hidden { get; set; } // Mark of the hidden order
        public string iceberg { get; set; } // Mark of the iceberg order
        public string visibleSize { get; set; } // displayed quantity for iceberg order
        public string cancelAfter { get; set; } // cancel orders time，requires timeInForce to be GTT
        public string channel { get; set; } // order source
        public string clientOid { get; set; } // user-entered order unique mark
        public string remark { get; set; } // remark
        public string tags { get; set; } // Tag order source
        public string isActive { get; set; } // order status, true and false. If true, the order is active, if false, the order is fillled or cancelled
        public string cancelExist { get; set; } // order cancellation transaction record
        public string createdAt { get; set; } // Time the order created
        public string tradeType { get; set; } //   The type of trading
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
