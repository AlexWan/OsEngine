using System.Collections.Generic;

namespace OsEngine.Market.Servers.HTX.Spot.Entity
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
            public string tradeId { get; set; }
            public string price { get; set; }
            public string amount { get; set; }
            public string direction { get; set; }
        }
    }

    public class ResponseChannelBook
    {
        public string ch { get; set; }        
        public Tick tick { get; set; }
        public string ts { get; set; }

        public class Tick
        {
            public List<List<string>> asks { get; set; }
            public List<List<string>> bids { get; set; }
            
        }
    }

    public class ResponseChannelUpdateOrder
    {
        public Data data { get; set; }
        public string code { get; set; }
        public class Data
        {
            public string accountId { get; set; }
            public string orderPrice { get; set; }
            public string orderSize { get; set; }
            public string orderCreateTime { get; set; }
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

    public class ResponseChannelUpdateMyTrade
    {
        public Data data { get; set; }
        public string code { get; set; }
        public class Data
        {
            public string eventType { get; set; }
            public string symbol { get; set; }
            public string orderId { get; set; }
            public string tradePrice { get; set; }
            public string tradeVolume { get; set; }
            public string orderSide { get; set; }
            public string tradeId { get; set; }
            public string tradeTime { get; set; }
            public string accountId { get; set; }
            public string source { get; set; }
            public string orderPrice { get; set; }
            public string orderSize { get; set; }
            public string clientOrderId { get; set; }
            public string orderCreateTime { get; set; }
            public string orderStatus { get; set; }
        }
    }    
}

