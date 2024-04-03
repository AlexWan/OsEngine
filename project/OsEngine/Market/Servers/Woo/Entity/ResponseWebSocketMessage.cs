using OsEngine.Entity;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.Woo.Entity
{
    public class ResponseWebSocketMessage<T>
    {
        public string topic;
    }

    public class ResponseChannelTrades
    {

        public Data data { get; set; }
        public string ts { get; set; }


        public class Data
        {
            public string symbol { get; set; }
            public string price { get; set; }
            public string size { get; set; }
            public string side { get; set; }
            public string source { get; set; }
        }
    }

    public class ResponseChannelBook
    {
        public Data data { get; set; }

        public string ts { get; set; }

        public class Data
        {
            public List<List<string>> asks { get; set; }
            public List<List<string>> bids { get; set; }
            public string ts { get; set; }
            public string symbol { get; set; }
        }
    }

    public class ResponseChannelPortfolio
    {
        public Data data { get; set; }

        public class Data
        {
            public Dictionary<string, Symbol> balances { get; set; }
        }
        public class Symbol
        {
            public string holding { get; set; }
            public string frozen { get; set; }
        }
    }

    public class ResponseChannelUpdateOrder
    {
        public Data data { get; set; }
        public class Data
        {
            public string msgType { get; set; }
            public string symbol { get; set; }
            public string orderId { get; set; }
            public string side { get; set; }
            public string quantity { get; set; }
            public string executedPrice { get; set; }
            public string price { get; set; }
            public string status { get; set; }
            public string clientOrderId { get; set; }
            public string executedQuantity { get; set; }
            public string type { get; set; }
            public string timestamp { get; set; }
            public string tradeId { get; set; }
        }
    }

    public class ResponseChannelUpdatePositions
    {
        public Data data { get; set; }

        public class Data
        {
            public Dictionary<string, Symbol> positions { get; set; }
        }

        public class Symbol
        {
            public string holding { get; set; }
        }       
    }
}

