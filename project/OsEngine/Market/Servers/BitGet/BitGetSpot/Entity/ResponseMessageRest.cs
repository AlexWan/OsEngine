using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitGet.BitGetSpot.Entity
{
    public class ResponseRestMessage<T>
    {
        public string code { get; set; }
        public string msg { get; set; }
        public string requestTime { get; set; }
        public T data { get; set; }
    }

    public class RestMessageCandle
    {
        public string code { get; set; }
        public string msg { get; set; }

        public string requestTime { get; set; }
        public List<List<string>> data { get; set; }
    }

    public class RestMessageSymbol
    {
        public string symbol;
        public string baseCoin;
        public string quoteCoin;  // "USDT"
        public string minTradeAmount;
        public string maxTradeAmount;
        public string takerFeeRate;
        public string makerFeeRate;
        public string quantityPrecision;
        public string quotePrecision;
        public string pricePrecision;
        public string status;
        public string minTradeUSDT;
        public string buyLimitPriceRatio;
        public string sellLimitPriceRatio;
    }

    public class RestMessageAccount
    {
        public string coin;
        public string frozen;
        public string available;
    }

    public class RestMessagePositions
    {
        public string marginCoin;
        public string symbol;
        public string holdSide;
        public string openDelegateCount;
        public string margin;
        public string available;
        public string locked;
        public string total;
        public string leverage;
        public string achievedProfits;
        public string averageOpenPrice;
        public string marginMode;
        public string holdMode;
        public string unrealizedPL;
        public string liquidationPrice;
        public string keepMarginRate;
        public string marketPrice;
        public string cTime;
    }

    public class RestMessageOrders
    {
        public string userId { get; set; }
        public string symbol { get; set; }
        public string orderId { get; set; }
        public string clientOid { get; set; }
        public string priceAvg { get; set; }
        public string price { get; set; }
        public string size { get; set; }
        public string orderType { get; set; }
        public string side { get; set; }
        public string status { get; set; }
        public string basePrice { get; set; }
        public string baseVolume { get; set; }
        public string quoteVolume { get; set; }
        public string enterPointSource { get; set; }
        public string presetTakeProfitPrice { get; set; }
        public string executeTakeProfitPrice { get; set; }
        public string presetStopLossPrice { get; set; }
        public string executeStopLossPrice { get; set; }
        public string cTime { get; set; }
        public string uTime { get; set; }
        public string tpslType { get; set; }
        public string triggerPrice { get; set; }
    }

    public class DataOrderStatus
    {
        public string symbol;
        public string size;
        public string orderId;
        public string clientOid;
        public string price;
        public string status;
        public string side;
        public string orderType;
        public string cTime;
    }

    public class RestMyTradesResponce
    {
        public string code;

        public string msg;

        public List<DataMyTrades> data;
    }

    public class DataMyTrades
    {
        public string tradeId;
        public string symbol;
        public string orderId;
        public string price;
        public string size;
        public FeeDetail feeDetail;
        public string side;
        public string cTime;
        public string priceAvg;
    }

    public class FeeDetail
    {
        public string feeCoin;
        public string totalFee;
    }

    public class TradeData
    {
        public string symbol { get; set; }
        public string tradeId { get; set; }
        public string side { get; set; }
        public string price { get; set; }
        public string size { get; set; }
        public string ts { get; set; }
    }
}