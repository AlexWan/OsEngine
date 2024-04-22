using System.Collections.Generic;

namespace OsEngine.Market.Servers.HTX.Swap.Entity
{ 
    public class ResponseChannelTrades
    {
        public Tick tick {  get; set; }
       
        public string ch {  get; set; }

        public class Tick
        {
            public List<Data> data { get; set; }
        }
        
        public class Data
        {
            public string ts { get; set; }
            public string id { get; set; }
            public string price { get; set; }
            public string amount { get; set; }
            public string direction { get; set; }
        }
    }

    public class ResponseChannelBook
    {
        public string ch { get; set; }
        public string ts { get; set; }      
        public Tick tick { get; set; }        

        public class Tick
        {
            public List<List<string>> asks { get; set; }
            public List<List<string>> bids { get; set; }
            public string ts { get; set; }

        }
    }

    public class ResponseChannelUpdateOrder
    {
        public List<Trade> trade { get; set; }
        public string symbol { get; set; }
        public string contract_type { get; set; }
        public string contract_code { get; set; }
        public string volume { get; set; }
        public string price { get; set; }
        public string direction { get; set; }
        public string offset { get; set; }
        public string status { get; set; }
        public string order_id { get; set; }
        public string ts { get; set; }
        public string created_at { get; set; }
        public string client_order_id { get; set; }
        public class Trade
        {
            public string id { get; set; }
            public string created_at { get; set; }
            public string trade_volume { get; set; }
            public string trade_price { get; set; }
            public string orderSource { get; set; }
            public string eventType { get; set; }
            public string symbol { get; set; }
            public string clientOrderId { get; set; }
            public string orderStatus { get; set; }
            public string orderId { get; set; }
            public string type { get; set; }
            public string lastActTime { get; set; }
        }
    }

    public class ResponseChannelPortfolio
    {
        public List<Data> data { get; set; }

        public class Data
        {
            public string symbol { get; set; }
            public string margin_balance { get; set; }
            public string margin_frozen { get; set; }
            public string margin_asset { get; set; }
        }
    }

    public class ResponseChannelUpdatePositions
    {
        public List<Data> data { get; set; }

        public class Data
        {
            public string symbol { get; set; }
            public string contract_code { get; set; }
            public string volume { get; set; }
            public string frozen { get; set; }           
        }
    }

    public class ResponsePingPrivate
    {
        public string ts { get; set; }
    }

    public class ResponsePingPublic
    {
        public string ping { get; set; }
    }
}

