namespace OsEngine.Market.Servers.Mexc.MexcSpot.Entity
{
    public class MyTradeWebSocket
    {
        public string channel { get; set; }
        public MexcSocketMyTrade privateDeals { get; set; }
        public string symbol { get; set; }
        public string sendTime { get; set; }
    }

    public class MexcSocketMyTrade
    {
        public string price { get; set; }
        public string quantity { get; set; }
        public string amount { get; set; }
        public string tradeType { get; set; }
        public string tradeId { get; set; }
        public string clientOrderId { get; set; }
        public string orderId { get; set; }
        public string feeAmount { get; set; }
        public string feeCurrency { get; set; }
        public string time { get; set; }
    }
}
