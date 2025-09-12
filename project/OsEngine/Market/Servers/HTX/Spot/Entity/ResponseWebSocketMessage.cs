using System.Collections.Generic;

namespace OsEngine.Market.Servers.HTX.Spot.Entity
{

    public class ResponseWebSocketMessage<T>
    {
        public string ch { get; set; }
        public string ts { get; set; }
        public T tick { get; set; }
    }

    public class ResponsePing
    {
        public string ping { get; set; }
    }

    public class ResponsePingPrivate
    {
        public string action { get; set; }
        public PingData data { get; set; }
    }

    public class PingData
    {
        public string ts { get; set; }
    }

    public class TradesData
    {
        public string id { get; set; }
        public string ts { get; set; }
        public List<ResponseTrades> data { get; set; }
    }

    public class ResponseTrades
    {
        public string id { get; set; }
        public string ts { get; set; }
        public string tradeId { get; set; }
        public string price { get; set; }
        public string amount { get; set; }
        public string direction { get; set; }
    }

    public class ResponseDepth
    {
        public List<List<string>> asks { get; set; }
        public List<List<string>> bids { get; set; }
    }

    public class ResponseChannelUpdateOrder
    {
        public Data data { get; set; }
        public string code { get; set; }

        public class Data
        {
            public string eventType { get; set; } // Event type, valid value: trade
            public string symbol { get; set; } // Trading symbol
            public string totalTradeAmount { get; set; }
            public string tradePrice { get; set; } // Trade price
            public string tradeVolume { get; set; } // Trade volume
            public string accountId { get; set; } // account ID
            public string orderPrice { get; set; } // Original order price (not available for market order)
            public string orderSize { get; set; } // Original order amount (not available for buy-market order)
            public string orderCreateTime { get; set; } // Order creation time
            public string orderSource { get; set; } // Order source
            public string clientOrderId { get; set; } // Client order ID (if any)
            public string orderStatus { get; set; } // Order status, valid value: partial-filled, filled
            public string orderId { get; set; } // Order ID
            public string type { get; set; }  // Order type
            public string tradeTime { get; set; }  // Trade time
            public string lastActTime { get; set; } // Order trigger time
            public string execAmt { get; set; }
            public string remainAmt { get; set; }
            public string canceledSource { get; set; }
            public string canceledSourceDesc { get; set; }
        }
    }

    public class ResponseMyTrade
    {
        public MyTradeData data { get; set; }
        public string code { get; set; }

    }

    public class MyTradeData
    {
        public string eventType { get; set; } // Event type (trade)
        public string symbol { get; set; } // Trading symbol
        public string orderId { get; set; } // Order ID
        public string tradePrice { get; set; } // Trade price
        public string tradeVolume { get; set; } // Trade volume
        public string orderSide { get; set; } // Order side, valid value: buy, sell
        public string orderType { get; set; } // Order type
        public string aggressor { get; set; } // Aggressor or not, valid value: true, false
        public string tradeId { get; set; } // Trade ID
        public string tradeTime { get; set; } // Trade time, unix time in millisecond
        public string transactFee { get; set; } // Transaction fee (positive value) or Transaction rebate (negative value)
        public string feeCurrency { get; set; }
        public string feeDeduct { get; set; } // Transaction fee deduction
        public string feeDeductType { get; set; } // Transaction fee deduction type, valid value: ht, point
        public string accountId { get; set; } // Account ID
        public string source { get; set; } // Order source
        public string orderPrice { get; set; } // Order price (invalid for market order)
        public string orderSize { get; set; } // Order size (invalid for market buy order)
        public string orderValue { get; set; } // Order value (only valid for market buy order)
        public string clientOrderId { get; set; } // Client order ID
        public string stopPrice { get; set; } // Stop price (only valid for stop limit order)
        public string orderCreateTime { get; set; } // Order creation time
        public string orderStatus { get; set; } // Order status, valid value: filled, partial-filled
    }

    public class TickerItem
    {
        public string open { get; set; }
        public string high { get; set; }
        public string low { get; set; }
        public string close { get; set; }
        public string amount { get; set; }
        public string vol { get; set; }
        public string count { get; set; }
        public string bid { get; set; }
        public string bidSize { get; set; }
        public string ask { get; set; }
        public string askSize { get; set; }
        public string lastPrice { get; set; }
        public string lastSize { get; set; }
    }
}

