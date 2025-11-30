using OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.Binance.Futures.Entity
{
    public class BinanceTime
    {
        public long serverTime { get; set; }
    }

    public class BinanceUserMessage
    {
        public string MessageStr;
    }

    public class ErrorMessage
    {
        public int code { get; set; }
        public string msg { get; set; }
    }

    public class HistoryOrderReport
    {

        public string symbol;
        public string orderId;
        public string clientOrderId;
        public string price;
        public string origQty;
        public string executedQty;
        public string cummulativeQuoteQty;
        public string status;
        public string timeInForce;
        public string type;
        public string side;
        public string stopPrice;
        public string icebergQty;
        public string time;
        public string updateTime;
        public string isWorking;

    }

    public class OrderActionResponse
    {
        public string clientOrderId; // ": "testOrder",
        public string cumQty; // ": "0",
        public string cumQuote; // ": "0",
        public string executedQty; // ": "0",
        public string orderId; // ": 22542179,
        public string avgPrice; // ": "0.00000",
        public string origQty; // ": "10",
        public string price; // ": "0",
        public string reduceOnly; // ": false,
        public string side; // ": "BUY",
        public string positionSide; // ": "SHORT",
        public string status; // ": "NEW",
        public string stopPrice; // ": "9300",		// please ignore when order type is TRAILING_STOP_MARKET
        public string closePosition; // ": false,   // if Close-All
        public string symbol; // ": "BTCUSDT",
        public string timeInForce; // ": "GTD",
        public string type; // ": "TRAILING_STOP_MARKET",
        public string origType; // ": "TRAILING_STOP_MARKET",
        public string activatePrice; // ": "9020",	// activation price, only return with TRAILING_STOP_MARKET order
        public string priceRate; // ": "0.3",			// callback rate, only return with TRAILING_STOP_MARKET order
        public string updateTime; // ": 1566818724722,
        public string workingType; // ": "CONTRACT_PRICE",
        public string priceProtect; // ": false,      // if conditional order trigger is protected	
        public string priceMatch; // ": "NONE",              //price match mode
        public string selfTradePreventionMode; // ": "NONE", //self trading preventation mode
        public string goodTillDate; // ": 1693207680000      //order pre-set auot cancel time for TIF GTD order
    }

    public class SecurityResponse
    {
        public string timezone { get; set; }
        public long serverTime { get; set; }
        public List<RateLimit> rateLimits { get; set; }
        public List<object> exchangeFilters { get; set; }
        public List<Symbol> symbols { get; set; }
    }

    public class ListenKey
    {
        public string listenKey { get; set; }
    }
}
