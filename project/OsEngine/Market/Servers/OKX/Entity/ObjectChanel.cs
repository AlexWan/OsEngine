using System.Collections.Generic;


namespace OsEngine.Market.Servers.OKX.Entity
{

    public class ObjectChanel<T>
    {
        public EventObjectChanelArgs arg;
        public List<T> data;
    }

    public class PositonsResponce
    {
        public string code;
        public string msg;
        public List<PositionResponseData> data;
    }

    public class OrdersResponce
    {
        public string code;
        public string msg;
        public List<OrderResponseData> data;
    }

    public class EventObjectChanelArgs
    {
        public string chanel;
        public string instType;
        public string uid;
    }

    public class PositionResponseData
    {
        public string adl;
        public string availPos;
        public string avgPx;
        public string baseBal;
        public string cTime;
        public string ccy;
        public string deltaBS;
        public string deltaPA;
        public string gammaBS;
        public string gammaPA;
        public string imr;
        public string instId;
        public string instType;
        public string interest;
        public string last;
        public string lever;
        public string liab;
        public string liabCcy;
        public string liqPx;
        public string margin;
        public string markPx;
        public string mgnMode;
        public string mgnRatio;
        public string mmr;
        public string notionalUsd;
        public string optVal;
        public string pTime;
        public string pendingCloseOrdLiabVal;
        public string pos;
        public string posCcy;
        public string posId;
        public string posSide;
        public string quoteBal;
        public string spotInUseAmt;
        public string spotInUseCcy;
        public string thetaBS;
        public string thetaPA;
        public string tradeId;
        public string uTime;
        public string upl;
        public string uplRatio;
        public string usdPx;
        public string vegaBS;
        public string vegaPA;
    }

    public class OrderResponseData
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
}
