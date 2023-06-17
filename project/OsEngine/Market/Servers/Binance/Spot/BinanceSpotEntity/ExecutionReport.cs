namespace OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity
{
    public class ExecutionReport
    {
        public string e { get; set; }// Event type
        public string E { get; set; }// Event time
        public string s { get; set; }// Symbol
        public string c { get; set; }// Client order ID
        public string S { get; set; }// Side
        public string o { get; set; }// Order type
        public string f { get; set; }// Time in force
        public string q { get; set; }// Order quantity
        public string p { get; set; }// Order price
        public string P { get; set; }// Stop price
        public string F { get; set; }// Iceberg quantity
        public string g { get; set; }// OrderListId
        public string C { get; set; }// Original client order ID; This is the ID of the order being canceled
        public string x { get; set; }// Current execution type
        public string X { get; set; }// Current order status
        public string r { get; set; }// Order reject reason; will be an error code.
        public string i { get; set; }// Order ID
        public string l { get; set; }// Last executed quantity
        public string z { get; set; }// Cumulative filled quantity
        public string L { get; set; }// Last executed price
        public string n { get; set; }// Commission amount
        public object N { get; set; }// Commission asset
        public string T { get; set; }// Transaction time
        public string t { get; set; }// Trade ID
        public string I { get; set; }// Ignore
        public bool w { get; set; }// Is the order on the book?
        public bool m { get; set; }// Is this trade the maker side?
        public bool M { get; set; }// Ignore
        public string O { get; set; }// Order creation time
        public string Z { get; set; }// Cumulative quote asset transacted quantity
        public string Q { get; set; }// Quote Order Quantity
    }
}