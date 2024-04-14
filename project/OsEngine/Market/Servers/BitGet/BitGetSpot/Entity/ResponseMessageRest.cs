namespace OsEngine.Market.Servers.BitGet.BitGetSpot.Entity
{
    public class ResponseMessageRest<T>
    {
        public string code;
        public string msg;
        public T data;
    }

    public class ResponseAsset
    {
        public string coinId;
        public string coinName;
        public string available;
        public string frozen;
        public string Lock;
        public string utime;
    }

    public class ResposeSymbol
    {
        public string symbol;
        public string symbolName;
        public string baseCoin;
        public string quoteCoin;
        public string minTradeAmount;
        public string maxTradeAmount;
        public string takerFeeRate;
        public string makerFeeRate;
        public string priceScale;
        public string quantityScale;
        public string minTradeUSDT;
        public string status;
        public string buyLimitPriceRatio;
        public string sellLimitPriceRatio;
    }

    public class ResponseCandle
    {
        public string open;
        public string high;
        public string low;
        public string close;
        public string quoteVol;
        public string baseVol;
        public string usdtVol;
        public string ts;
    }

    public class ResponseMyTrade
    {
        public string accountId;
        public string symbol;
        public string orderId;
        public string fillId;
        public string orderType;
        public string side;
        public string fillPrice;
        public string fillQuantity;
        public string fillTotalAmount;
        public string cTime;
        public string feeCcy;
        public string fees;
    }

    public class ResponseOrder
    {
        public string accountId;
        public string symbol;
        public string orderId;
        public string clientOrderId;
        public string price;
        public string quantity;
        public string orderType;
        public string side;
        public string status;
        public string fillPrice;
        public string fillQuantity;
        public string fillTotalAmount;
        public string enterPointSource;
        public string cTime;
    }
}
