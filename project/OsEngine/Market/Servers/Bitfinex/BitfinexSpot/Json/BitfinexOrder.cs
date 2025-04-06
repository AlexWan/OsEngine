
namespace OsEngine.Market.Servers.Bitfinex.Json
{
    public class BitfinexOrderData
    {
        public string Id { get; set; }                   //1747566428,Order ID
        public string Gid { get; set; }                  //null, Group Order ID
        public string Cid { get; set; }                  //1678987199446,  Client Order ID
        public string Symbol { get; set; }               //"tBTCUSD",Symbol
        public string MtsCreate { get; set; }            //1678988263843,Millisecond epoch timestamp of creation
        public string MtsUpdate { get; set; }            //1678988263843, Millisecond epoch timestamp of last update
        public string Amount { get; set; }               //-0.25, Amount, Positive means buy, negative means sell
        public string AmountOrig { get; set; }           //0.1, //Original amount (before any update)
        public string OrderType { get; set; }            //"exchange limit", The order's type 
        public string TypePrev { get; set; }             //"exchange limit", Previous order type
        public string MtsTif { get; set; }               //null, Millisecond epoch timestamp for TIF (Time-In-Force)
        public string Flags { get; set; }                // 0, Sum of all active flags for the order
        public string Status { get; set; }               // "Active",Status
        public string Price { get; set; }                // 25000, Price
        public string PriceAvg { get; set; }             // 0,153, Average price
        public string PriceTrailing { get; set; }        // 0,The trailing price
        public string PriceAuxLimit { get; set; }        // 0, Auxiliary Limit price (for STOP LIMIT)
        public string Notify { get; set; }               // 0, 1 if operations on order must trigger a notification, 0 if operations on order must not trigger a notification
        public string Hidden { get; set; }               // 0, 1 if order must be hidden, 0 if order must not be hidden
        public string PlacedId { get; set; }             // null, If another order caused this order to be placed (OCO) this will be that other order's ID
        public string Routing { get; set; }              //  "API>BFX", Indicates origin of action
    }
}