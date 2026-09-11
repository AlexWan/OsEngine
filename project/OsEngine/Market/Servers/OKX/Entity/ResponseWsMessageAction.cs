using System.Collections.Generic;

namespace OsEngine.Market.Servers.OKX.Entity
{
    public class ResponseWsMessageAction<T>
    {
        public string @event { get; set; }
        public string code { get; set; }
        public string msg { get; set; }
        // "snapshot"/"update" for the books channel
        public string action { get; set; }
        public ResponseWsMessageArg arg { get; set; }
        public T data { get; set; }
    }

    public class ResponseWsMessageArg
    {
        public string channel { get; set; }
        public string instId { get; set; }
    }

    // first-pass routing header: no data field,
    // so Newtonsoft skips the payload without building a JToken tree
    public class ResponseWsMessageHeader
    {
        public string @event { get; set; }
        public string code { get; set; }
        public string msg { get; set; }
        public ResponseWsMessageArg arg { get; set; }
    }

    // order book item: full 5-level books5 rows, or incremental books rows ([price, size, ...]).
    // seqId/prevSeqId are present only for the books channel
    public class ResponseWsDepthItem
    {
        public string seqId { get; set; }
        public string prevSeqId { get; set; }
        public string ts { get; set; }
        public List<List<string>> asks { get; set; }
        public List<List<string>> bids { get; set; }
    }

    public class ResponseWsTrade
    {
        public string instId { get; set; }
        public string tradeId { get; set; }
        public string px { get; set; }
        public string sz { get; set; }
        public string side { get; set; }
        public string ts { get; set; }
    }

    public class ResponseWebSocketMessageSubscribe
    {
        public string Event { get; set; }
        public string code { get; set; }
        public string msg { get; set; }
    }
    public class ResponseWsOrders
    {
        public string accFillSz { get; set; }
        public string algoClOrdId { get; set; }
        public string algoId { get; set; }
        public string amendResult { get; set; }
        public string amendSource { get; set; }
        public string avgPx { get; set; }
        public string cancelSource { get; set; }
        public string category { get; set; }
        public string ccy { get; set; }
        public string clOrdId { get; set; }
        public string code { get; set; }
        public string cTime { get; set; }
        public string execType { get; set; }
        public string fee { get; set; }
        public string feeCcy { get; set; }
        public string fillFee { get; set; }
        public string fillFeeCcy { get; set; }
        public string fillNotionalUsd { get; set; }
        public string fillPx { get; set; }
        public string fillSz { get; set; }
        public string fillPnl { get; set; }
        public string fillTime { get; set; }
        public string fillPxVol { get; set; }
        public string fillPxUsd { get; set; }
        public string fillMarkVol { get; set; }
        public string fillFwdPx { get; set; }
        public string fillMarkPx { get; set; }
        public string fillIdxPx { get; set; }
        public string instId { get; set; }
        public string instType { get; set; }
        public string lever { get; set; }
        public string msg { get; set; }
        public string notionalUsd { get; set; }
        public string ordId { get; set; }
        public string ordType { get; set; }
        public string pnl { get; set; }
        public string posSide { get; set; }
        public string px { get; set; }
        public string pxUsd { get; set; }
        public string pxVol { get; set; }
        public string pxType { get; set; }
        public string quickMgnType { get; set; }
        public string rebate { get; set; }
        public string rebateCcy { get; set; }
        public string reqId { get; set; }
        public string side { get; set; }
        public string attachAlgoClOrdId { get; set; }
        public string slOrdPx { get; set; }
        public string slTriggerPx { get; set; }
        public string slTriggerPxType { get; set; }
        public string source { get; set; }
        public string state { get; set; }
        public string stpId { get; set; }
        public string stpMode { get; set; }
        public string sz { get; set; }
        public string tag { get; set; }
        public string tdMode { get; set; }
        public string tgtCcy { get; set; }
        public string tpOrdPx { get; set; }
        public string tpTriggerPx { get; set; }
        public string tpTriggerPxType { get; set; }
        public string tradeId { get; set; }
        public string tradeQuoteCcy { get; set; }
        public string lastPx { get; set; }
        public string uTime { get; set; }
        public string isTpLimit { get; set; }
        public List<object> attachAlgoOrds { get; set; }
        public LinkedAlgoOrd linkedAlgoOrd { get; set; }
    }

    public class LinkedAlgoOrd
    {
        public string algoId { get; set; }
    }

    public class ResponseWsAccount
    {
        public List<PortfolioDetails> details { get; set; }
        public string totalEq { get; set; }
    }

    public class PortfolioDetails
    {
        public string availBal { get; set; }
        public string ccy { get; set; }
        public string frozenBal { get; set; }
        public string upl { get; set; }
        public string eq { get; set; }
    }

    public class ResponseMessagePositions
    {
        public string instType { get; set; }
        public string instId { get; set; }
        public string posSide { get; set; }
        public string availPos { get; set; }
        public string pos { get; set; }
        public string upl { get; set; }
        public string realizedPnl { get; set; }
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
