using System.Collections.Generic;

namespace OsEngine.Market.Servers.OKX.Entity
{
    public class ResponseRestMessage<T>
    {
        public string code { get; set; }
        public string msg { get; set; }
        public T data { get; set; }
        public string inTime { get; set; }
        public string outTime { get; set; }
    }

    public class RestMessageSendOrder
    {
        public string sCode { get; set; }
        public string sMsg { get; set; }
    }

    public class RestMessageCandle
    {
        public List<List<string>> data { get; set; }
    }

    public class RestMessageSymbol
    {
        public string symbol { get; set; }
        public string makerFeeRate { get; set; }
        public string takerFeeRate { get; set; }
        public string feeRateUpRatio { get; set; }
        public string openCostUpRatio { get; set; }
        public string quoteCoin { get; set; }
        public string baseCoin { get; set; }
        public string buyLimitPriceRatio { get; set; }
        public string sellLimitPriceRatio { get; set; }
        public List<string> supportMarginCoins { get; set; }
        public string minTradeNum { get; set; }
        public string priceEndStep { get; set; }
        public string volumePlace { get; set; }
        public string sizeMultiplier { get; set; }
        public string symbolType { get; set; }
        public string symbolStatus { get; set; }
        public string offTime { get; set; }
        public string limitOpenTime { get; set; }
        public string maintainTime { get; set; }
        public string pricePlace { get; set; }
    }

    public class RestMessageAccount
    {
        public string marginCoin { get; set; }
        public string locked { get; set; }
        public string available { get; set; }
        public string crossMaxAvailable { get; set; }
        public string fixedMaxAvailable { get; set; }
        public string maxTransferOut { get; set; }
        public string equity { get; set; }
        public string usdtEquity { get; set; }
        public string btcEquity { get; set; }
        public string crossRiskRate { get; set; }
        public string crossMarginLeverage { get; set; }
        public string fixedLongLeverage { get; set; }
        public string fixedShortLeverage { get; set; }
        public string marginMode { get; set; }
        public string holdMode { get; set; }
        public string unrealizedPL { get; set; }
        public string bonus { get; set; }
    }

    public class RestMessagePositions
    {
        public string marginCoin { get; set; }
        public string symbol { get; set; }
        public string holdSide { get; set; }
        public string openDelegateCount { get; set; }
        public string margin { get; set; }
        public string available { get; set; }
        public string locked { get; set; }
        public string total { get; set; }
        public string leverage { get; set; }
        public string achievedProfits { get; set; }
        public string averageOpenPrice { get; set; }
        public string marginMode { get; set; }
        public string holdMode { get; set; }
        public string unrealizedPL { get; set; }
        public string liquidationPrice { get; set; }
        public string keepMarginRate { get; set; }
        public string marketPrice { get; set; }
        public string cTime { get; set; }
    }

    public class RestMessageOrders
    {
        public List<EntrustedList> entrustedList { get; set; }
    }

    public class EntrustedList
    {
        public string symbol { get; set; }
        public string baseVolume { get; set; }
        public string orderId { get; set; }
        public string clientOid { get; set; }
        public string filledQty { get; set; }
        public string fee { get; set; }
        public string price { get; set; }
        public string status { get; set; }
        public string side { get; set; }
        public string timeInForce { get; set; }
        public string totalProfits { get; set; }
        public string posSide { get; set; }
        public string marginCoin { get; set; }
        public string presetTakeProfitPrice { get; set; }
        public string presetStopLossPrice { get; set; }
        public string filledAmount { get; set; }
        public string orderType { get; set; }
        public string leverage { get; set; }
        public string marginMode { get; set; }
        public string size { get; set; }
        public string holdMode { get; set; }
        public string tradeSide { get; set; }
        public string cTime { get; set; }
        public string uTime { get; set; }
    }

    public class DataOrderStatus
    {
        public string symbol { get; set; }
        public string size { get; set; }
        public string orderId { get; set; }
        public string clientOid { get; set; }
        public string price { get; set; }
        public string state { get; set; }
        public string side { get; set; }
        public string posSide { get; set; }
        public string posMode { get; set; }
        public string orderType { get; set; }
        public string cTime { get; set; }
        public string marginCoin { get; set; }
    }

    public class RestMyTradesResponce
    {
        public string code { get; set; }

        public string msg { get; set; }

        public DataMyTrades data { get; set; }
    }

    public class DataMyTrades
    {
        public List<FillList> fillList { get; set; }
    }
    public class FillList
    {
        public string tradeId { get; set; }
        public string symbol { get; set; }
        public string orderId { get; set; }
        public string price { get; set; }
        public string baseVolume { get; set; }
        public string fee { get; set; }
        public string side { get; set; }
        public string fillAmount { get; set; }
        public string profit { get; set; }
        public string enterPointSource { get; set; }
        public string tradeSide { get; set; }
        public string holdMode { get; set; }
        public string takerMakerFlag { get; set; }
        public string cTime { get; set; }
    }

    public class FundingItemHistory
    {
        public string formulaType { get; set; }
        public string fundingRate { get; set; }
        public string fundingTime { get; set; }
        public string instId { get; set; }
        public string instType { get; set; }
        public string method { get; set; }
        public string realizedRate { get; set; }
    }

    public class AccountConfigData
    {
        public string posMode { get; set; }
    }
}
