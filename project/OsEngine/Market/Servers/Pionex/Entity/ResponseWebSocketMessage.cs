using System.Collections.Generic;


namespace OsEngine.Market.Servers.Pionex.Entity
{
    public class ResponseWebSocketMessage<T>
    {
        public string result;
        public string topic;
        public string symbol;
        public string type;
        public string timestamp;
        public T data;
    }

    public class ResponseWebSocketTrades
    {
        public List<TradeElements> trades;
    }

    public class TradeElements
    {
        public string symbol;
        public string tradeId;
        public string price;
        public string size;
        public string side;
        public string timestamp;
    }

    public class ResponseWebSocketDepthItem
    {
        public List<List<string>> asks;
        public List<List<string>> bids;
    }

    public class ResponseWSBalance
    {
        public List<BalanceWS> balances;
    }

    public class BalanceWS
    {
        public string coin;
        public string free;    // Available balance, 8 decimal digits.
        public string frozen;  // Frozen balance, 8 decimal digits.
    }

    public class MyTrades
    {
        public string id;
        public string orderId;
        public string symbol;
        public string side;
        public string role;
        public string price;
        public string size;
        public string fee;
        public string feeCoin;
        public string timestamp;
    }
    public class MyOrders
    {
        public string orderId;
        public string symbol;
        public string type;         // LIMIT / MARKET.
        public string side;
        public string price;
        public string size;         // Order quantity.
        public string filledSize;   // Filled quantity of order.
        public string filledAmount; // Filled amount of order.
        public string fee;          // Transaction fee.
        public string feeCoin;      // Currency of transaction fee.
        public string IOC;
        public string status;       // OPEN / CLOSED.
        public string clientOrderId;
        public string createTime;
        public string updateTime;
    }
}
