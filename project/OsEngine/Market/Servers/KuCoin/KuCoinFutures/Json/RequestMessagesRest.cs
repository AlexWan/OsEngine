namespace OsEngine.Market.Servers.KuCoin.KuCoinFutures.Json
{
    public class SendOrderRequestData
    {
        public string clientOid;
        public string symbol;
        public string side;
        public string type;
        public string price;
        public string size;
        public string leverage;
        public string closeOrder;
        public string marginMode;
        public string positionSide;
    }

    public class CancelAllOrdersRequestData
    {
        public string symbol;
    }
}