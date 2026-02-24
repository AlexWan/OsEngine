namespace OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity
{
    public class ExecutionReportEvent
    {
        public string subscriptionId { get; set; }
        public ExecutionReport @event { get; set; }
    }

    public class ExecutionReport
    {
        public string e { get; set; }      // Event type
        public string E { get; set; }        // Event time (Unix ms)
        public string s { get; set; }      // Symbol
        public string c { get; set; }      // Client order ID
        public string S { get; set; }      // Side (BUY/SELL)
        public string o { get; set; }      // Order type
        public string f { get; set; }      // Time in force
        public string q { get; set; }      // Order quantity
        public string p { get; set; }      // Order price
        public string P { get; set; }      // Stop price
        public string F { get; set; }      // Iceberg quantity
        public string g { get; set; }         // OrderListId
        public string C { get; set; }      // Original client order ID (order being canceled)
        public string x { get; set; }      // Current execution type
        public string X { get; set; }      // Current order status
        public string r { get; set; }      // Order reject reason
        public string i { get; set; }        // Order ID
        public string l { get; set; }      // Last executed quantity
        public string z { get; set; }      // Cumulative filled quantity
        public string L { get; set; }      // Last executed price
        public string n { get; set; }      // Commission amount
        public string N { get; set; }      // Commission asset (can be null)
        public string T { get; set; }        // Transaction time (Unix ms)
        public string t { get; set; }        // Trade ID
        public string v { get; set; }         // Prevented Match Id (visible if order expired due to STP)
        public string I { get; set; }        // Execution Id
        public string w { get; set; }        // Is order on the book?
        public string m { get; set; }        // Is this trade the maker side?
        public string M { get; set; }        // Ignore
        public string O { get; set; }        // Order creation time (Unix ms)
        public string Z { get; set; }      // Cumulative quote asset transacted quantity
        public string Y { get; set; }      // Last quote asset transacted quantity
        public string Q { get; set; }      // Quote Order Quantity
        public string W { get; set; }        // Working time (visible if order on book)
        public string V { get; set; }      // SelfTradePreventionMode
    }
}