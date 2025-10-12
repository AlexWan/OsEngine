namespace OsEngine.Market.Servers.XT.XTFutures.Entity
{
    public class XTFuturesSendOrderRequestData
    {
        public string clientOrderId { get; set; }       //client order id
        public string symbol { get; set; }               //"btc_usdt", symbol
        public string orderSide { get; set; }                //BUY,SELL
        public string orderType { get; set; }                //order type:LIMIT,MARKET
        public string origQty { get; set; }            //Quantity (Cont)
        public string price { get; set; }               //price.
        public string timeInForce { get; set; }         //Valid way:GTC;IOC;FOK;GTX
        public string positionSide { get; set; }          //Position side:LONG;SHORT   
        public string triggerProfitPrice { get; set; }  //Stop profit price
        public string triggerStopPrice { get; set; }    //Stop loss price
    }

    public class XTFuturesCancelAllOrdersRequestData
    {
        public string symbol { get; set; }              //"btc_usdt", symbol
        public string bizType { get; set; }             //SPOT, LEVER
    }
}
