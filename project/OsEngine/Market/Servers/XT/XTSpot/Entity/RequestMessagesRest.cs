namespace OsEngine.Market.Servers.XT.XTSpot.Entity
{
    public class SendOrderRequestData
    {
        public string symbol { get; set;}               //"btc_usdt", symbol
        public string clientOrderId { get; set; }       //client order id
        public string side { get; set; }                //BUY,SELL
        public string type { get; set; }                //order type:LIMIT,MARKET
        public string timeInForce { get; set; }         //effective way:GTC, FOK, IOC, GTX (for Market order use only FOK or IOC. Not GTC!)
        public string bizType { get; set; }             //SPOT, LEVER
        public string price { get; set; }               //price. Required if it is the LIMIT price; blank if it is the MARKET price
        public string quantity { get; set; }            //quantity. Required if it is the LIMIT price or the order is placed at the market price by quantity
        public string quoteQty { get; set; }            //amount. Required if it is the LIMIT price or the order is the market price when placing an order by amount
    }

    public class CancelAllOrdersRequestData
    {
        public string symbol { get; set; }              //"btc_usdt", symbol
        public string bizType { get; set; }             //SPOT, LEVER
    }
}
