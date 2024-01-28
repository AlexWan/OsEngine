using System.Collections.Generic;


namespace OsEngine.Market.Servers.KuCoin.KuCoinSpot.Json
{
    public class SendOrderRequestData
    {
        public string clientOid;
        public string symbol;
        public string side;
        public string type;
        public string price;
        public string size;
    }

    public class CancelAllOrdersRequestData
    {
        public string symbol;
    }



}
