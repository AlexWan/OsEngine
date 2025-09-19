using System.Collections.Generic;


namespace OsEngine.Market.Servers.XT.XTSpot.Entity
{
    public class ResponseMessageRest<T>
    {
        public string rc { get; set; } // Response code
        public string mc { get; set; } // Message code
                                       // public List<string> ma { get; set; }
        public T result { get; set; }
    }

    public class ResponseServerTime
    {
        public string serverTime { get; set; } //"1662444177871" server time in ms 
    }

    public class ResponseDepth
    {
        public string timestamp { get; set; } //"1662444177871" in ms
        public string lastUpdateId { get; set; } //"1662444177871" in ms
        public List<List<string>> bids { get; set; } //List of bids
        public List<List<string>> asks { get; set; } //List of asks
    }

    public class ResponseToken
    {
        public string accessToken { get; set; } //Private API access token
        public string refreshToken { get; set; }
    }

    public class ResponseSymbols
    {
        public string time { get; set; } //1662444177871 in ms,
        public string version { get; set; } //Version number, when the request version number is consistent with the response content version,
                                            //the list will not be returned, reducing IO eg: 2e14d2cd5czcb2c2af2c1db 
        public List<ResponseSymbol> symbols { get; set; } //List of symbols
    }

    public class ResponseCandles
    {
        public List<ResponseCandle> candles { get; set; } //List of candles
    }

    public class ResponseCandle
    {
        public string t { get; set; } //1662601014832, open time in ms
        public string o { get; set; } //"30000", open price
        public string c { get; set; } //"32000", close price
        public string h { get; set; } //"35000", highest price
        public string l { get; set; } //"25000", lowest price
        public string q { get; set; } //"512", transaction quantity
        public string v { get; set; } //"15360000", transaction volume
    }

    public class ResponsePlaceOrder
    {
        public string orderId { get; set; } //Placed Order Id
    }

    public class ResponseAsset
    {
        public string currency { get; set; } //Asset currency
        public string currencyId { get; set; } //Asset currency Id
        public string frozenAmount { get; set; } //Frozen amount
        public string availableAmount { get; set; } //Available amount
        public string totalAmount { get; set; } //Total amount
        public string convertBtcAmount { get; set; } //BTC amount
    }

    public class ResponseAssets
    {
        public string totalUsdtAmount { get; set; }
        public string totalBtcAmount { get; set; }
        public List<ResponseAsset> assets { get; set; } //List of assets
    }

    public class ResponseSymbol
    {
        public string id { get; set; } //Id
        public string symbol { get; set; } //"btc_usdt", Symbol
        public string displayName { get; set; } //Display name
        public string state { get; set; } //symbol state [ONLINE;OFFLINE,DELISTED]
        public string tradingEnabled { get; set; } //Trading is available or not
        public string openapiEnabled { get; set; } //Openapi transaction is available or not
        public string nextStateTime { get; set; }
        public string nextState { get; set; }
        public string depthMergePrecision { get; set; } //Depth Merge Accuracy
        public string baseCurrency { get; set; } //"btc"
        public string baseCurrencyPrecision { get; set; } //"8"
        public string baseCurrencyId { get; set; } //"2"
        public string quoteCurrency { get; set; } //"usdt"
        public string quoteCurrencyPrecision { get; set; } //"4"
        public string quoteCurrencyId { get; set; } //"11"
        public string pricePrecision { get; set; } //"4", Transaction price accuracy
        public string quantityPrecision { get; set; } //"6", Transaction quantity accuracy
        public List<string> orderTypes { get; set; } //List of Order Types [LIMIT;MARKET]
        public List<string> timeInForces { get; set; } //List of Effective Ways [GTC, IOC, FOK, GTX=Revoke if unable to become a pending party]
        public string displayWeight { get; set; } //Show the weight, the greater the weight, the more forward 
        public string displayLevel { get; set; } //Presentation level, [FULL=Full display,SEARCH=Search display,DIRECT=Direct display,NONE=Don't show]
        public List<Filter> filters { get; set; } //List of filters
    }

    public class Filter
    {
        public string filter { get; set; }
        public string buyMaxDeviation { get; set; }
        public string sellMaxDeviation { get; set; }
        public string maxDeviation { get; set; }
        public string durationSeconds { get; set; }
        public string maxPriceMultiple { get; set; }
        public string min { get; set; }
        public string max { get; set; }
        public string tickSize { get; set; }
    }

    public class ResponseMyTrade
    {
        public string symbol { get; set; } //"BTC_USDT",
        public string tradeId { get; set; } //"6316559590087222001", Trade Id
        public string orderId { get; set; } //"6216559590087220004", Order Id
        public string orderSide { get; set; } //"BUY", SELL
        public string orderType { get; set; } //"LIMIT", MARKET
        public string bizType { get; set; } //"SPOT",
        public string time { get; set; } //"1655958915583", time in ms
        public string price { get; set; } //"40000", price
        public string quantity { get; set; } //"1.2", volume
        public string quoteQty { get; set; } //"48000", amount
        public string baseCurrency { get; set; } //"BTC",
        public string quoteCurrency { get; set; } //"USDT",
        public string fee { get; set; } //"0.5",
        public string feeCurrency { get; set; } //"USDT",
        public string takerMaker { get; set; } //"taker", takerMaker
    }

    public class ResponseMyTrades
    {
        public string hasPrev { get; set; } //"true", boolean
        public string hasNext { get; set; } //"true", boolean
        public List<ResponseMyTrade> items { get; set; } //List of my trades
    }

    public class CancaledOrderResponse
    {
        public string cancelId { get; set; } //"6216559590087220004", Canceled Order Id
    }

    public class OrderResponse
    {
        public string symbol { get; set; } //"symbol": "BTC_USDT",
        public string orderId { get; set; } //"orderId": "6216559590087220004",
        public string clientOrderId { get; set; } //"clientOrderId": "16559590087220001",
        public string baseCurrency { get; set; } //"baseCurrency": "string",
        public string quoteCurrency { get; set; } //"quoteCurrency": "string",
        public string side { get; set; } //"side": "BUY,SELL"
        public string type { get; set; } //"type": "LIMIT,MARKET"
        public string timeInForce { get; set; } //"timeInForce": "GTC,IOC,FOK,GTX"
        public string price { get; set; } //"price": "40000",
        public string origQty { get; set; } //"origQty": "2",
        public string origQuoteQty { get; set; } //"origQuoteQty": "48000",
        public string executedQty { get; set; } //"executedQty": "1.2",
        public string leavingQty { get; set; } //"leavingQty": "The quantity to be executed (if the order is cancelled or the order is rejected, the value is 0)"
        public string tradeBase { get; set; } //"tradeBase": "2", transaction quantity
        public string tradeQuote { get; set; } //"tradeQuote": "48000",
        public string avgPrice { get; set; } //"avgPrice": "42350",
        public string fee { get; set; } //"fee": "string",
        public string feeCurrency { get; set; } //"feeCurrency": "string",
        public string state { get; set; } //"state": "NEW,PARTIALLY_FILLED,FILLED,CANCELED,REJECTED,EXPIRED"
        public string time { get; set; } //"time": 1655958915583,
        public string updateTime { get; set; } //"updatedTime": 1655958915583
    }
}
