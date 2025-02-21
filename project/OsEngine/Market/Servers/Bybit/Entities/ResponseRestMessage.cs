using System.Collections.Generic;

namespace OsEngine.Market.Servers.Bybit.Entities
{
    public class ResponseRestMessage<T>
    {
        public string retCode { get; set; }
        public string retMsg { get; set; }
        public T result { get; set; }
        public RetExtInfo retExtInfo { get; set; }
        public string time { get; set; }

    }
    public class ResponseRestMessageList<T>
    {
        public string retCode { get; set; }
        public string retMsg { get; set; }
        public RetResalt<T> result { get; set; }
        public RetExtInfo retExtInfo { get; set; }
        public string time { get; set; }

    }
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class LeverageFilter
    {
        public string minLeverage { get; set; }
        public string maxLeverage { get; set; }
        public string leverageStep { get; set; }
    }

    public class Symbols
    {
        public string symbol { get; set; }
        public string contractType { get; set; }
        public string status { get; set; }
        public string baseCoin { get; set; }
        public string quoteCoin { get; set; }
        public string launchTime { get; set; }
        public string deliveryTime { get; set; }
        public string deliveryFeeRate { get; set; }
        public string priceScale { get; set; }
        public LeverageFilter leverageFilter { get; set; }
        public PriceFilter priceFilter { get; set; }
        public LotSizeFilter lotSizeFilter { get; set; }
        public string unifiedMarginTrade { get; set; }
        public string fundingInterval { get; set; }
        public string settleCoin { get; set; }
        public string copyTrading { get; set; }
    }

    public class LotSizeFilter
    {
        public string maxOrderQty { get; set; }
        public string minOrderQty { get; set; }
        public string qtyStep { get; set; }
        public string postOnlyMaxOrderQty { get; set; }
    }

    public class PriceFilter
    {
        public string minPrice { get; set; }
        public string maxPrice { get; set; }
        public string tickSize { get; set; }
    }

    public class ArraySymbols
    {
        public string category { get; set; }
        public List<Symbols> list { get; set; }
        public string nextPageCursor { get; set; }
    }

    public class RetExtInfo
    {
    }

    public class RetResalt<T>
    {
        public string category { get; set; }
        public List<T> list { get; set; }
    }

    public class RetTrade
    {
        public string execId { get; set; }
        public string symbol { get; set; }
        public string price { get; set; }
        public string size { get; set; }
        public string side { get; set; }
        public string time { get; set; }
        public string isBlockTrade { get; set; }
    }

    public class PositionOnBoardResult
    {
        public string symbol; // ": "ADAUSDT",
        public string leverage; // ": "10",
        public string autoAddMargin; // ": 0,
        public string avgPrice; // ": "0.3594",
        public string liqPrice; // ": "",
        public string riskLimitValue; // ": "200000",
        public string takeProfit; // ": "",
        public string positionValue; // ": "0.3594",
        public string isReduceOnly; // ": false,
        public string tpslMode; // ": "Full",
        public string riskId; // ": 116,
        public string trailingStop; // ": "0",
        public string unrealisedPnl; // ": "0.0001",
        public string markPrice; // ": "0.3595",
        public string adlRankIndicator; // ": 2,
        public string cumRealisedPnl; // ": "-0.0624684",
        public string positionMM; // ": "0.00301896",
        public string createdTime; // ": "1707043707699",
        public string positionIdx; // ": 0,
        public string positionIM; // ": "0.03626346",
        public string seq; // ": 162587161997,
        public string updatedTime; // ": "1730270283779",
        public string side; // ": "Buy",
        public string bustPrice; // ": "",
        public string positionBalance; // ": "0",
        public string leverageSysUpdatedTime; // ": "",
        public string curRealisedPnl; // ": "-0.0003594",
        public string size; // ": "1",
        public string positionStatus; // ": "Normal",
        public string mmrSysUpdatedTime; // ": "",
        public string stopLoss; // ": "",
        public string tradeMode; // ": 0,
        public string sessionAvgPrice; // ": ""
    }
}
