﻿using System.Collections.Generic;

namespace OsEngine.Market.Servers.BingX.BingXSpot.Entity
{
    public class ResponseSpotBingX<T>
    {
        public string code { get; set; }
        public string msg { get; set; }
        public string debugMsg { get; set; }
        public T data { get; set; }
    }

    public class BalanceArray
    {
        public List<BalanceData> balances { get; set; }
    }
    public class SymbolArray
    {
        public List<SymbolData> symbols { get; set; }
    }

    public class BalanceData
    {
        public string asset; // Asset name
        public string free; // Available asset
        public string locked; // Freeze asset
    }

    public class ResponseErrorCode
    {
        public string code { get; set; }
        public string msg { get; set; }
        public string success { get; set; }
        public string timeStamp { get; set; }

    }

    public class CandlestickChartData
    {
        public string code { get; set; }
        public string timestamp { get; set; }
        public List<string[]> data { get; set; }
    }

    public class SymbolData
    {
        public string symbol { get; set; } // Trading pair
        public string minQty { get; set; } // Version upgrade, this field is deprecated, please ignore this field
        public string maxQty { get; set; } // Version upgrade, this field is deprecated, please ignore this field 
        public string minNotional { get; set; } // Minimum transaction amount
        public string maxNotional { get; set; } // Maximum transaction amount
        public string status { get; set; } // 0 offline, 1 online, 5 pre-open, 25 trading suspended
        public string tickSize { get; set; } // Price step
        public string stepSize { get; set; } // Quantity step
        public string apiStateSell { get; set; } // available sell via api
        public string apiStateBuy { get; set; } // available buy via api
        public string timeOnline { get; set; } // online time
    }

    public class ResponseCreateOrder
    {
        public string symbol { get; set; } // Trading pair
        public string orderId { get; set; } // Order ID
        public string transactTime { get; set; } // Transaction timestamp
        public string price { get; set; } // Price
        public string origQty { get; set; } // Original quantity
        public string executedQty { get; set; } // Executed quantity
        public string cummulativeQuoteQty { get; set; } // Cumulative quote asset transacted quantity
        public string status { get; set; } // Order status: NEW, PENDING, PARTIALLY_FILLED, FILLED, CANCELED, FAILED
        public string type { get; set; } // MARKET/LIMIT
        public string side { get; set; } // BUY/SELL
        public string clientOrderID { get; set; } // Customized order ID for users
    }
}

