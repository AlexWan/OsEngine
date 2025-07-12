using System.Collections.Generic;

namespace OsEngine.Market.Servers.OKX.Entity
{
    public class ResponseWsMessageAction<T>
    {
        public string @event;
        public string msg;
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

    public class ResponseWsGreeks
    {
        public string instType { get; set; }
        public string instId { get; set; }
        public string uly { get; set; }
        public string delta { get; set; }
        public string gamma { get; set; }
        public string vega { get; set; }
        public string theta { get; set; }
        public string lever { get; set; }
        public string markVol { get; set; }
        public string bidVol { get; set; }
        public string askVol { get; set; }
        public string realVol { get; set; }
        public string deltaBS { get; set; }
        public string gammaBS { get; set; }
        public string thetaBS { get; set; }
        public string vegaBS { get; set; }
        public string ts { get; set; }
        public string fwdPx { get; set; }
        public string volLv { get; set; }
    }

    public class ResponseWsOpenInterest
    {
        public string instType { get; set; }
        public string instId { get; set; }
        public string oi { get; set; }
        public string oiCcy { get; set; }
        public string ts { get; set; }
    }

    public class ResponseWsMarkPrice
    {
        public string instType { get; set; }
        public string instId { get; set; }
        public string markPx { get; set; }
        public string ts { get; set; }
    }

    public class FundingItem
    {
        public string formulaType { get; set; }
        public string fundingRate { get; set; }
        public string fundingTime { get; set; }
        public string impactValue { get; set; }
        public string instId { get; set; }
        public string instType { get; set; }
        public string interestRate { get; set; }
        public string method { get; set; }
        public string maxFundingRate { get; set; }
        public string minFundingRate { get; set; }
        public string nextFundingRate { get; set; }
        public string nextFundingTime { get; set; }
        public string premium { get; set; }
        public string settFundingRate { get; set; }
        public string settState { get; set; }
        public string ts { get; set; }
    }

    public class TickerItem
    {
        public string instType { get; set; }
        public string instId { get; set; }
        public string last { get; set; }
        public string lastSz { get; set; }
        public string askPx { get; set; }
        public string askSz { get; set; }
        public string bidPx { get; set; }
        public string bidSz { get; set; }
        public string open24h { get; set; }
        public string high24h { get; set; }
        public string low24h { get; set; }
        public string volCcy24h { get; set; }
        public string vol24h { get; set; }
        public string sodUtc0 { get; set; }
        public string sodUtc8 { get; set; }
        public string ts { get; set; }
    }
}
