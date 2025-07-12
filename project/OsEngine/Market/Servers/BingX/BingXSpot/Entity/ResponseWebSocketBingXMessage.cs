using System.Collections.Generic;

namespace OsEngine.Market.Servers.BingX.BingXSpot.Entity
{
    public class ResponseWebSocketBingXMessage<T>
    {
        public string code { get; set; }
        public string msg { get; set; }
        public string id { get; set; }
        public string timestamp { get; set; }
        public T data { get; set; }
        public string dataType { get; set; }
        public string success { get; set; }
    }

    public class SubscriptionOrderUpdateData
    {
        public string e { get; set; } //Event Type
        public string E { get; set; } // event time
        public string s { get; set; } // trading pair
        public string S { get; set; } // Order direction
        public string o { get; set; } // order type
        public string q { get; set; } // Order original quantity
        public string p { get; set; } // Original order price
        public string x { get; set; } // Event Type
        public string X { get; set; } // order status
        public string i { get; set; } // Order ID
        public string l { get; set; } // Last order transaction volume
        public string z { get; set; } // Accumulated transaction volume of orders
        public string L { get; set; } // Last transaction price of the order
        public string n { get; set; } // Number of handling fees
        public string N { get; set; } // Handling fee asset category
        public string T { get; set; } // transaction time
        public string t { get; set; } // Transaction ID
        public string O { get; set; } // Order creation time
        public string Z { get; set; } // Accumulated transaction amount of orders
        public string Y { get; set; } // Last transaction amount of the order
        public string Q { get; set; } // Original order amount
        public string m { get; set; }
        public string C { get; set; } // Client ID
    }

    public class MarketDepthDataMessage
    {
        public List<string[]> bids;
        public List<string[]> asks;
    }

    public class ResponseWebSocketTrade
    {
        public string e { get; set; } // Event Type
        public string E { get; set; } // event time
        public string s { get; set; } // trading pair
        public string t { get; set; } // Transaction ID
        public string p { get; set; } // transaction price
        public string q { get; set; } // Executed quantity
        public string T { get; set; } // transaction time
        public string m { get; set; } // Whether the buyer is a market maker. If true, this transaction is an active sell order, otherwise it is an active buy order
    }

    public class TickerItem
    {
        public string e { get; set; }
        public string E { get; set; }
        public string s { get; set; }
        public string p { get; set; }
        public string P { get; set; }
        public string o { get; set; }
        public string h { get; set; }
        public string l { get; set; }
        public string c { get; set; }
        public string v { get; set; }
        public string q { get; set; }
        public string O { get; set; }
        public string C { get; set; }
        public string B { get; set; }
        public string b { get; set; }
        public string A { get; set; }
        public string a { get; set; }
        public string n { get; set; }
    }
}
