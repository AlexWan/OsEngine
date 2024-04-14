using OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    public class SecurityResponce
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
