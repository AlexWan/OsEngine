namespace OsEngine.Market.Servers.Bitfinex.BitfitnexEntity
{
    class BitfinexOrder
    {
        public long OrderId { get; set; }
        public string Symbol { get; set; }
        public string Price { get; set; }
        public decimal AverageExecutionPrice { get; set; }
        public string Side { get; set; }
        public string Type { get; set; }
        public string Timestamp { get; set; }
        public bool IsLive { get; set; }
        public bool IsCancelled { get; set; }
        public bool IsHidden { get; set; }
        public bool WasForced { get; set; }
        public decimal OriginalAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public decimal ExecutedAmount { get; set; }
    }

    public class BitfinexResponseOrder
    {
        public long id { get; set; }
        public long cid { get; set; }
        public string cid_date { get; set; }
        public object gid { get; set; }
        public string symbol { get; set; }
        public string exchange { get; set; }
        public string price { get; set; }
        public string avg_execution_price { get; set; }
        public string side { get; set; }
        public string type { get; set; }
        public string timestamp { get; set; }
        public bool is_live { get; set; }
        public bool is_cancelled { get; set; }
        public bool is_hidden { get; set; }
        public object oco_order { get; set; }
        public bool was_forced { get; set; }
        public string original_amount { get; set; }
        public string remaining_amount { get; set; }
        public string executed_amount { get; set; }
        public string src { get; set; }
        public long order_id { get; set; }
    }
}
