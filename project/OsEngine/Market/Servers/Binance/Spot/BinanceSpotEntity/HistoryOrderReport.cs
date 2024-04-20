namespace OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity
{
    public class HistoryOrderReport
    {

        public string symbol;
        public string orderId;
        public string clientOrderId;
        public string price;
        public string origQty;
        public string executedQty;
        public string cummulativeQuoteQty;
        public string status;
        public string timeInForce;
        public string type;
        public string side;
        public string stopPrice;
        public string icebergQty;
        public string time;
        public string updateTime;
        public string isWorking;

    }

    public class HistoryMyTradeReport
    {
        public string symbol; // ": "BNBBTC",
        public string id; // ": 28457,
        public string orderId; // ": 100234,
        public string orderListId; // ": -1, //Unless OCO, the value will always be -1
        public string price; // ": "4.00000100",
        public string qty; // ": "12.00000000",
        public string quoteQty; // ": "48.000012",
        public string commission; // ": "10.10000000",
        public string commissionAsset; // ": "BNB",
        public string time; // ": 1499865549590,
        public string isBuyer; // ": true,
        public string isMaker; // ": false,
        public string isBestMatch; // ": true
    }
}