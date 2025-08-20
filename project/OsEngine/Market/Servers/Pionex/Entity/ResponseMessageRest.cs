using System.Collections.Generic;

namespace OsEngine.Market.Servers.Pionex.Entity
{
    public class ResponseMessageRest<T>
    {
        public string result;
        public string code;
        public string message;
        public T data;
        public string timestamp;

    }

    public class ResponseSymbols
    {
        public List<Symbol> symbols;
    }

    public class Symbol
    {
        public string symbol; // BTC_USDT
        public string type; // SPOT 
        public string baseCurrency; // BTC         // Base coin.
        public string quoteCurrency; // USDT       // Quote coin.
        public string basePrecision; // 6          // Precision digits of base currency price.
        public string quotePrecision;  // 2        // Precision digits of quote currency price.
        public string amountPrecision; // 8        // Precision digits of the amount of market price buying order.
        public string minAmount; // 10             // Minimum amount of the order, only for SPOT
        public string minTradeSize; // 0.000001    // Minimum limit order quantity.
        public string maxTradeSize; // 1000        // Maximum limit order quantity.
        public string minTradeDumping; // 0.000001 // Minimum quantity of market price selling order.
        public string maxTradeDumping; // 100      // Maximum quantity of market price selling order.
        public string enable; // True              // Enable trading.
        public string buyCeiling; // 1.1           // Maximum ratio of buying price, cannot be higher than a multiple of the latest highest buying price.
        public string sellFloor; // 0.9            // Minimum ratio of selling price, cannot be lower than a multiple of the latest lowest selling price.
    }

    public class ResponseBalance
    {
        public List<Balance> balances;
    }

    public class Balance
    {
        public string coin;
        public string free; // Available balance, 8 decimal digits.
        public string frozen;    // Frozen balance, 8 decimal digits.
    }

    public class ResponseCandles
    {
        public List<Kline> klines;
    }

    public class Kline
    {
        public string time;
        public string open;
        public string close;
        public string high;
        public string low;
        public string volume;
    }

    public class ResponseCreateOrder
    {
        public string orderId;
        public string clientOrderId;
    }

    public class OrderData
    {
        public List<GetOrder> orders { get; set; }
    }

    public class GetOrder
    {
        public string orderId { get; set; }
        public string symbol { get; set; }
        public string type { get; set; }
        public string side { get; set; }
        public string price { get; set; }
        public string size { get; set; }
        public string filledSize { get; set; }
        public string filledAmount { get; set; }
        public string fee { get; set; }
        public string feeCoin { get; set; }
        public string status { get; set; }
        public string IOC { get; set; }
        public string clientOrderId { get; set; }
        public string source { get; set; }
        public string createTime { get; set; }
        public string updateTime { get; set; }
    }

    public class FillData
    {
        public List<FillItem> fills { get; set; }
    }

    public class FillItem
    {
        public string id { get; set; }
        public string orderId { get; set; }
        public string symbol { get; set; }
        public string side { get; set; }
        public string role { get; set; }
        public string price { get; set; }
        public string size { get; set; }
        public string fee { get; set; }
        public string feeCoin { get; set; }
        public string timestamp { get; set; }
    }
}
