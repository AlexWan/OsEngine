using System.Collections.Generic;

namespace OsEngine.Market.Servers.OKX.Entity
{
    public class ResponseWsMessageAction<T>
    {
        public ResponseWsMessageArg arg;
        public T data;
    }

    public class ResponseWsMessageArg
    {
        public string channel;
        public string instId;
    }

    public class ResponseWsDepthItem
    {
        public List<List<string>> asks;
        public List<List<string>> bids;
        public string ts;
    }

    public class ResponseWsTrade
    {
        public string instId;
        public string tradeId;
        public string px;
        public string sz;
        public string side;
        public string ts;
    }

    public class ResponseWebSocketMessageSubscribe
    {
        public string Event;
        public string code;
        public string msg;
    }
    public class ResponseWsOrders
    {
        public string accFillSz;
        public string amendResult;
        public string avgPx;
        public string cTime;
        public string category;
        public string ccy;
        public string clOrdId;
        public string code;
        public string execType;
        public string fee;
        public string feeCcy;
        public string fillFee;
        public string fillFeeCcy;
        public string fillNotionalUsd;
        public string fillPx;
        public string fillSz;
        public string fillTime;
        public string instId;
        public string instType;
        public string lever;
        public string msg;
        public string notionalUsd;
        public string ordId;
        public string ordType;
        public string pnl;
        public string posSide;
        public string px;
        public string rebate;
        public string rebateCcy;
        public string reduceOnly;
        public string reqId;
        public string side;
        public string slOrdPx;
        public string slTriggerPx;
        public string slTriggerPxType;
        public string source;
        public string state;
        public string sz;
        public string tag;
        public string tdMode;
        public string tgtCcy;
        public string tpOrdPx;
        public string tpTriggerPx;
        public string tpTriggerPxType;
        public string tradeId;
        public string uTime;
    }

    public class ResponseWsAccount
    {
        public List<PortfolioDetails> details;
        public string totalEq;
    }

    public class PortfolioDetails
    {
        public string availBal;
        public string ccy;
        public string frozenBal;
        public string upl;
        public string eq;
    }

    public class ResponseMessagePositions
    {
        public string instId;
        public string posSide;
        public string availPos;
        public string pos;
        public string upl;
        public string realizedPnl;
    }
}
