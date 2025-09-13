namespace OsEngine.Market.Servers.MoexFixFastTwimeFutures.Entity
{
    public class OrderChange
    {
        public string NameID { get; set; }
        public int RptSeq { get; set; }
        public string MDEntryID { get; set; }
        public OrderAction Action { get; set; }
        public OrderType OrderType { get; set; }
        public decimal Price { get; set; }
        public decimal Volume { get; set; }
    }

    public enum OrderAction
    {
        Add,
        Change,
        Delete,
        None
    }

    public enum OrderType
    {
        Bid,
        Ask,
        None
    }
}

