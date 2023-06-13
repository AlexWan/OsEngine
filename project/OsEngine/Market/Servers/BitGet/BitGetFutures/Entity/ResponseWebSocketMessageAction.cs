using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitGet.BitGetFutures.Entity
{
    public class ResponseWebSocketMessageAction<T>
    {
        public string action;
        public ResponseWebSocketMessageArg arg;
        public T data;
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
        public string accFillSz;
        public string avgPx;
        public string cTime;
        public string clOrdId;
        public string eps;
        public string force;
        public string hM;
        public string instId;
        public string lever;
        public string low;
        public string notionalUsd;
        public string ordId;
        public string ordType;
        public string posSide;
        public string px;
        public string side;
        public string status;
        public string sz;
        public string tS;
        public string tdMode;
        public string tgtCcy;
        public string uTime;
        public string tradeId;
        public string fillSz;
        public string fillTime;
        public string fillPx;
    }
}
