using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitGet.BitGetFutures.Entity
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
        public string instType;
        public string channel;
        public string instId;
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
        public string orderType;
        public string status;
        public string instId;
        public string cTime;
        public string clientOId;
        public string orderId;
        public string side;
        public string size;
        public string baseVolume;
        public string accBaseVolume;
        public string price;
        public string fillTime;
        public string tradeId;
        public string fillPrice;
        public string posMode;
        public string tradeSide;
        public string posSide;
    }

    public class ResponseWebSocketAccount
    {
        public string frozen;
        public string available;
        public string marginCoin;
    }

    public class ResponseMessagePositions
    {
        public string posId;
        public string instId;
        public string marginCoin;
        public string marginSize;
        public string marginMode;
        public string holdSide;
        public string posMode;
        public string total;
        public string available;
        public string frozen;
        public string openPriceAvg;
        public string leverage;
        public string achievedProfits;
        public string unrealizedPL;
        public string unrealizedPLR;
        public string liquidationPrice;
        public string keepMarginRate;
        public string marketPrice;
        public string cTime;
        public string breakEvenPrice;
        public string totalFee;
        public string deductedFee;
        public string uTime;
        public string autoMargin;
    }

    public class ResponseTicker
    {
        public string instId { get; set; }
        public string lastPr { get; set; }
        public string bidPr { get; set; }
        public string askPr { get; set; }
        public string bidSz { get; set; }
        public string askSz { get; set; }
        public string open24h { get; set; }
        public string high24h { get; set; }
        public string low24h { get; set; }
        public string change24h { get; set; }
        public string fundingRate { get; set; }
        public string nextFundingTime { get; set; }
        public string markPrice { get; set; }
        public string indexPrice { get; set; }
        public string holdingAmount { get; set; }
        public string baseVolume { get; set; }
        public string quoteVolume { get; set; }
        public string openUtc { get; set; }
        public string symbolType { get; set; }
        public string symbol { get; set; }
        public string deliveryPrice { get; set; }
        public string ts { get; set; }
    }
}
