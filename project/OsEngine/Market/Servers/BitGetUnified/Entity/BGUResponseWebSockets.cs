/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;


namespace OsEngine.Market.Servers.BitGetUnified.Entity
{
    public class BGUResponseWebSockets<T>
    {
        public T[] data { get; set; }
        public Arg arg { get; set; }
        public string action { get; set; }
        public string ts { get; set; }
    }

    public class Arg
    {
        public string instType { get; set; }
        public string topic { get; set; }
        public string symbol { get; set; }
    }

    public class BGUnifiedAccount
    {
        public string unrealisedPnL { get; set; }
        public string totalEquity { get; set; }
        public string positionMgnRatio { get; set; }
        public string mmr { get; set; }
        public string effEquity { get; set; }
        public string imr { get; set; }
        public string mgnRatio { get; set; }
        public BGUnifiedCoin[] coin { get; set; }
    }

    public class BGUnifiedCoin
    {
        public string debts { get; set; }
        public string balance { get; set; }
        public string available { get; set; }
        public string borrow { get; set; }
        public string locked { get; set; }
        public string equity { get; set; }
        public string coin { get; set; }
        public string usdValue { get; set; }
    }

    public class BGUnifiedDepth
    {
        public List<List<string>> a;
        public List<List<string>> b;
        public int pseq { get; set; }
        public long seq { get; set; }
        public string maxDepth { get; set; }
        public string ts { get; set; }
    }

    public class BGUnifiedPublicTrade
    {
        public string p { get; set; }
        public string S { get; set; }
        public string T { get; set; }
        public string v { get; set; }
        public string i { get; set; }
        public string L { get; set; }
        public string isRPI { get; set; }
    }

    public class BGUnifiedPositions
    {
        public string symbol { get; set; }
        public string leverage { get; set; }
        public string openFeeTotal { get; set; }
        public string mmr { get; set; }
        public string breakEvenPrice { get; set; }
        public string available { get; set; }
        public string liqPrice { get; set; }
        public string marginMode { get; set; }
        public string unrealisedPnl { get; set; }
        public string markPrice { get; set; }
        public string createdTime { get; set; }
        public string avgPrice { get; set; }
        public string totalFundingFee { get; set; }
        public string updatedTime { get; set; }
        public string marginCoin { get; set; }
        public string frozen { get; set; }
        public string profitRate { get; set; }
        public string closeFeeTotal { get; set; }
        public string marginSize { get; set; }
        public string curRealisedPnl { get; set; }
        public string size { get; set; }
        public string positionStatus { get; set; }
        public string posSide { get; set; }
        public string holdMode { get; set; }
    }

    public class BGUnifiedOrder
    {
        public string category { get; set; }
        public string symbol { get; set; }
        public string orderId { get; set; }
        public string clientOid { get; set; }
        public string price { get; set; }
        public string qty { get; set; }
        public string amount { get; set; }
        public string holdMode { get; set; }
        public string holdSide { get; set; }
        public string delegateType { get; set; }
        public string tradeSide { get; set; }
        public string orderType { get; set; }
        public string timeInForce { get; set; }
        public string side { get; set; }
        public string marginMode { get; set; }
        public string marginCoin { get; set; }
        public string reduceOnly { get; set; }
        public string cumExecQty { get; set; }
        public string cumExecValue { get; set; }
        public string avgPrice { get; set; }
        public string totalProfit { get; set; }
        public string orderStatus { get; set; }
        public string cancelReason { get; set; }
        public string leverage { get; set; }
        public Feedetail[] feeDetail { get; set; }
        public string createdTime { get; set; }
        public string updatedTime { get; set; }
        public string stpMode { get; set; }
    }

    public class Feedetail
    {
        public string feeCoin { get; set; }
        public string fee { get; set; }
    }

    public class BGUnifiedTrade
    {
        public string symbol { get; set; }
        public string orderType { get; set; }
        public string updatedTime { get; set; }
        public string side { get; set; }
        public string orderId { get; set; }
        public string execPnl { get; set; }
        public Feedetail[] feeDetail { get; set; }
        public string execTime { get; set; }
        public string tradeScope { get; set; }
        public string tradeSide { get; set; }
        public string execId { get; set; }
        public string execLinkId { get; set; }
        public string execPrice { get; set; }
        public string holdSide { get; set; }
        public string execValue { get; set; }
        public string category { get; set; }
        public string execQty { get; set; }
        public string clientOid { get; set; }
        public string isRPI { get; set; }
    }

    public class BGUnifiedTicker
    {
        public string bid1Price { get; set; }
        public string lowPrice24h { get; set; }
        public string ask1Size { get; set; }
        public string volume24h { get; set; }
        public string price24hPcnt { get; set; }
        public string highPrice24h { get; set; }
        public string turnover24h { get; set; }
        public string bid1Size { get; set; }
        public string ask1Price { get; set; }
        public string openPrice24h { get; set; }
        public string lastPrice { get; set; }
        public string indexPrice { get; set; }
        public string openInterest { get; set; }
        public string markPrice { get; set; }
        public string fundingRate { get; set; }
        public string nextFundingTime { get; set; }
        public string deliveryTime { get; set; }
        public string deliveryStartTime { get; set; }
        public string deliveryStatus { get; set; }
    }
}
