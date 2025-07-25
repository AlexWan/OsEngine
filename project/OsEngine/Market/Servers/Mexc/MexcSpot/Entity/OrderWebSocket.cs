namespace OsEngine.Market.Servers.Mexc.MexcSpot.Entity
{
    public class OrderWebSocket
    {
        public string channel { get; set; }
        public MexcSocketOrder privateOrders { get; set; }
        public string symbol { get; set; }
        public string sendTime { get; set; }
    }

    public class MexcSocketOrder
    {
        public string id { get; set; }
        public string clientId { get; set; }
        public string price { get; set; }
        public string quantity { get; set; }
        public string amount { get; set; }
        public string avgPrice { get; set; }
        public string orderType { get; set; }
        public string tradeType { get; set; }
        public string remainAmount { get; set; }
        public string remainQuantity { get; set; }
        public string lastDealQuantity { get; set; }
        public string cumulativeQuantity { get; set; }
        public string cumulativeAmount { get; set; }
        public string status { get; set; }
        public string createTime { get; set; }
    }
}
