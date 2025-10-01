using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitGet.BitGetSpot.Entity
{
    public class ResponseWebSocketMessageAction<T>
    {
        public string action { get; set; }
        public ResponseWebSocketMessageArg arg { get; set; }
        public T data { get; set; }
        public string ts { get; set; }
    }

    public class ResponseWebsocketTrade
    {
        public string ts;
        public string price;
        public string size;
        public string side;
        public string tradeId;
    }

    public class ResponseWebSocketMessageSubscribe
    {
        public string Event;
        public string code;
        public string msg;
    }

    public class ResponseWebSocketMessageArg
    {
        public string instType { get; set; }
        public string channel { get; set; }
        public string instId { get; set; }
    }

    public class ResponseWebSocketDepthItem
    {
        public List<List<string>> asks;
        public List<List<string>> bids;
        public int checksum;
        public string ts;
    }

    public class ResponseWebSocketOrder
    {
        public string instId { get; set; }
        public string orderId { get; set; }
        public string clientOid { get; set; }
        public string price { get; set; }
        public string size { get; set; }
        public string newSize { get; set; }
        public string notional { get; set; }
        public string orderType { get; set; }
        public string force { get; set; }
        public string side { get; set; }
        public string fillPrice { get; set; }
        public string tradeId { get; set; }
        public string baseVolume { get; set; }
        public string fillTime { get; set; }
        public string fillFee { get; set; }
        public string fillFeeCoin { get; set; }
        public string tradeScope { get; set; }
        public string accBaseVolume { get; set; }
        public string priceAvg { get; set; }
        public string status { get; set; }
        public string cTime { get; set; }
        public string uTime { get; set; }
        public string stpMode { get; set; }
        public List<FeeDetailOrder> feeDetail { get; set; }
        public string enterPointSource { get; set; }
    }

    public class FeeDetailOrder
    {
        public string feeCoin { get; set; }
        public string fee { get; set; }
    }

    public class ResponseWebSocketMyTrade
    {
        public string orderId { get; set; }
        public string tradeId { get; set; }
        public string symbol { get; set; }
        public string orderType { get; set; }
        public string side { get; set; }
        public string priceAvg { get; set; }
        public string size { get; set; }
        public string amount { get; set; }
        public string tradeScope { get; set; }
        public List<FeeDetailMyTrade> feeDetail { get; set; }
        public string cTime { get; set; }
        public string uTime { get; set; }
    }

    public class FeeDetailMyTrade
    {
        public string feeCoin { get; set; }
        public string deduction { get; set; }
        public string totalDeductionFee { get; set; }
        public string totalFee { get; set; }
    }

    public class ResponseWebSocketAccount
    {
        public string frozen;
        public string available;
        public string coin;
    }

    public class ResponseMessagePositions
    {
        public string marginCoin;
        public string instId;
        public string holdSide;
        public string frozen;
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
        public string instType;
    }

    public class TickerItem
    {
        public string instId { get; set; }
        public string lastPr { get; set; }
        public string open24h { get; set; }
        public string high24h { get; set; }
        public string low24h { get; set; }
        public string change24h { get; set; }
        public string bidPr { get; set; }
        public string askPr { get; set; }
        public string bidSz { get; set; }
        public string askSz { get; set; }
        public string baseVolume { get; set; }
        public string quoteVolume { get; set; }
        public string openUtc { get; set; }
        public string changeUtc24h { get; set; }
        public string ts { get; set; }
    }
}
