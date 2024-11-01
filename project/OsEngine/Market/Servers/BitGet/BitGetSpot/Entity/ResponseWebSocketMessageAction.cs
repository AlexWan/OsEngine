using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitGet.BitGetSpot.Entity
{
    public class ResponseWebSocketMessageAction<T>
    {
        public string action;
        public ResponseWebSocketMessageArg arg;
        public T data;
    }

    public class ResponseWebsocketTrade
    {
        public string ts;
        public string price;
        public string size;
        public string side;
        public string tradeId;
    }

    public class ResponseWebSocketMessageSubscrible
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
        public string clientOid;
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
        public string fillFee;
        public string fillFeeCoin;
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
}
