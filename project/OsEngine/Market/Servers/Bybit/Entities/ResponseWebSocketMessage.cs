using System.Collections.Generic;

namespace OsEngine.Market.Servers.Bybit.Entities
{
    public class ResponseWebSocketMessage<T>
    {
        public string topic;
        public string ts;
        public string type;
        public T data;
    }

    public class ResponseWebSocketMessageList<T>
    {
        public string topic;
        public string ts;
        public string type;
        public List<T> data;
    }

    public class ResponseWebSocketMyMessage<T>
    {
        public string id { get; set; }
        public string topic { get; set; }
        public string creationTime { get; set; }
        public T data { get; set; }
    }

    public class ResponseMyTrades
    {
        public string category { get; set; }
        public string symbol { get; set; }
        public string execFee { get; set; }
        public string execId { get; set; }
        public string execPrice { get; set; }
        public string execQty { get; set; }
        public string execType { get; set; }
        public string execValue { get; set; }
        public string isMaker { get; set; }
        public string feeRate { get; set; }
        public string tradeIv { get; set; }
        public string markIv { get; set; }
        public string blockTradeId { get; set; }
        public string markPrice { get; set; }
        public string indexPrice { get; set; }
        public string underlyingPrice { get; set; }
        public string leavesQty { get; set; }
        public string orderId { get; set; }
        public string orderLinkId { get; set; }
        public string orderPrice { get; set; }
        public string orderQty { get; set; }
        public string orderType { get; set; }
        public string stopOrderType { get; set; }
        public string side { get; set; }
        public string execTime { get; set; }
        public string isLeverage { get; set; }
        public string closedSize { get; set; }
        public string seq { get; set; }
    }

    public class SubscribeMessage
    {
        public string op;
        public string success;
        public string req_id;
        public string ret_msg;
        public string conn_id;
    }

    public class ResponseTrade
    {
        public string v;
        public string i;
        public string T;
        public string p;
        public string s;
        public string S;
        public string L;
        public string BT;
    }

    public class ResponseOrderBook
    {
        public string s;
        public string[,] b;
        public string[,] a;
    }

    public class ResponseOrder
    {
        public string symbol { get; set; }
        public string orderId { get; set; }
        public string side { get; set; }
        public string orderType { get; set; }
        public string cancelType { get; set; }
        public string price { get; set; }
        public string qty { get; set; }
        public string orderIv { get; set; }
        public string timeInForce { get; set; }
        public string orderStatus { get; set; }
        public string orderLinkId { get; set; }
        public string lastPriceOnCreated { get; set; }
        public string reduceOnly { get; set; }
        public string leavesQty { get; set; }
        public string leavesValue { get; set; }
        public string cumExecQty { get; set; }
        public string cumExecValue { get; set; }
        public string avgPrice { get; set; }
        public string blockTradeId { get; set; }
        public string positionIdx { get; set; }
        public string cumExecFee { get; set; }
        public string createdTime { get; set; }
        public string updatedTime { get; set; }
        public string rejectReason { get; set; }
        public string stopOrderType { get; set; }
        public string tpslMode { get; set; }
        public string triggerPrice { get; set; }
        public string takeProfit { get; set; }
        public string stopLoss { get; set; }
        public string tpTriggerBy { get; set; }
        public string slTriggerBy { get; set; }
        public string tpLimitPrice { get; set; }
        public string slLimitPrice { get; set; }
        public string triggerDirection { get; set; }
        public string triggerBy { get; set; }
        public string closeOnTrigger { get; set; }
        public string category { get; set; }
        public string placeType { get; set; }
        public string smpType { get; set; }
        public string smpGroup { get; set; }
        public string smpOrderId { get; set; }
        public string feeCurrency { get; set; }
    }

    public class ResponseTicker
    {
        public string symbol { get; set; }
        public string tickDirection { get; set; }
        public string price24hPcnt { get; set; }
        public string lastPrice { get; set; }
        public string prevPrice24h { get; set; }
        public string highPrice24h { get; set; }
        public string lowPrice24h { get; set; }
        public string prevPrice1h { get; set; }
        public string markPrice { get; set; }
        public string indexPrice { get; set; }
        public string openInterest { get; set; }
        public string openInterestValue { get; set; }
        public string turnover24h { get; set; }
        public string volume24h { get; set; }
        public string nextFundingTime { get; set; }
        public string fundingRate { get; set; }
        public string bid1Price { get; set; }
        public string bid1Size { get; set; }
        public string ask1Price { get; set; }
        public string ask1Size { get; set; }

        // option data
        public string markPriceIv { get; set; }
        public string underlyingPrice { get; set; }
        public string delta { get; set; }
        public string gamma { get; set; }
        public string vega { get; set; }
        public string theta { get; set; }
        public string askIv { get; set; }
        public string bidIv { get; set; }
    }
}
