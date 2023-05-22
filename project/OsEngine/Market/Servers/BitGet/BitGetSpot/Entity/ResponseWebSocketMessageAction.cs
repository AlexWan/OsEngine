using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitGet.BitGetSpot.Entity
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
        public string instId;
        public string ordId;
        public string clOrdId;
        public string px;
        public string sz;
        public string notional;
        public string ordType;
        public string force;
        public string side;
        public string fillPx;
        public string tradeId;
        public string fillSz;
        public string fillTime;
        public string fillFee;
        public string fillFeeCcy;
        public string execType;
        public string accFillSz;
        public string avgPx;
        public string status;
        public string cTime;
        public string uTime;
        public string eps;
    }
}
