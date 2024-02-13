using System.Collections.Generic;

namespace OsEngine.Market.Servers.BingX.BingXFutures.Entity
{
    public class ResponseWSBingXFuturesMessage<T>
    {
        public string code { get; set; }
        public string dataType { get; set; }
        public string ts { get; set; }
        public T data { get; set; }
    }

    public class MarketDepthDataMessage
    {
        public List<List<string>> bids { get; set; }
        public List<List<string>> asks { get; set; }
    }

    public class TradeUpdateEvent
    {
        public string e { get; set; } // Event type
        public string E { get; set; } // Event time
        public TradeOrderDetails o { get; set; } // Order details
    }

    public class TradeOrderDetails
    {
        public string s { get; set; } // trading pair:LINK-USDT
        public string c { get; set; } // client custom order ID
        public string i { get; set; } // Order ID:1627970445070303232
        public string S { get; set; } // order direction:SELL
        public string o { get; set; } // order type:MARKET
        public string q { get; set; } // order quantity:5.00000000
        public string p { get; set; } // order price:7.82700000
        public string sp { get; set; } // trigger price:7.82700000
        public string ap { get; set; } // order average price:7.82690000
        public string x { get; set; } // The specific execution type of this event:TRADE
        public string X { get; set; } // current status of the order:FILLED
        public string N { get; set; } // Fee asset type:USDT
        public string n { get; set; } // handling fee:-0.01369708
        public string T { get; set; } // transaction time:1676973375149
        public string wt { get; set; } // trigger price type: MARK_PRICE mark price, CONTRACT_PRICE latest price, INDEX_PRICE index price
        public string ps { get; set; } // Position direction: LONG or SHORT or BOTH
        public string rp { get; set; } // The transaction achieves profit and loss: 0.00000000
        public string z { get; set; } // Order Filled Accumulated Quantity: 0.00000000
    }

    public class SubscribeLatestTradeDetail<T>
    {
        public string code { get; set; }
        public string dataType { get; set; }
        public List<TradeDetails> data { get; set; }
    }

    public class TradeDetails
    {
        public string q { get; set; } // volume
        public string p { get; set; } // price
        public string T { get; set; } // transaction time
        public string m { get; set; } // Whether the buyer is a market maker. If true, this transaction is an active sell order, otherwise it is an active buy order.
        public string s { get; set; } // trading pair

    }

    public class AccountUpdateEvent
    {
        public string e { get; set; } // Event type
        public string E { get; set; } // Event time
        public AccountData a { get; set; } // Account data
    }

    public class AccountData
    {
        public string m { get; set; } // event launch reason
        public List<AssetBalance> B { get; set; } // Array: balance information
        public List<TradeInfo> P { get; set; } // Array: trade info
    }

    public class AssetBalance
    {
        public string a { get; set; } // asset name:USDT
        public string wb { get; set; } // wallet balance:5277.59264687
        public string cw { get; set; } // Wallet balance excluding isolated margin:5233.21709203
        public string bc { get; set; } // wallet balance change amount:0
    }

    public class TradeInfo
    {
        public string s { get; set; } // trading pair:LINK-USDT
        public string pa { get; set; } // position:108.84300000
        public string ep { get; set; } // entry price:7.25620000
        public string up { get; set; } // unrealized profit and loss of positions:1.42220000
        public string mt { get; set; } // margin mode:isolated
        public string iw { get; set; } // If it is an isolated position, the position margin:23.19081642
        public string ps { get; set; } // position direction:SHORT
    }
}
