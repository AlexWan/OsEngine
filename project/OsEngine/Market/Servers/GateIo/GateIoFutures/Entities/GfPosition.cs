
namespace OsEngine.Market.Servers.GateIo.GateIoFutures.Entities.Response
{
    public partial class GfPosition
    {
        public string user { get; set; }
        public string contract { get; set; }
        public string size { get; set; }
        public string leverage { get; set; }
        public string risk_limit { get; set; }
        public string leverage_max { get; set; }
        public string maintenance_rate { get; set; }
        public string value { get; set; }
        public string margin { get; set; }
        public string entry_price { get; set; }
        public string liq_price { get; set; }
        public string mark_price { get; set; }
        public string unrealised_pnl { get; set; }
        public string realised_pnl { get; set; }
        public string history_pnl { get; set; }
        public string last_close_pnl { get; set; }
        public string realised_point { get; set; }
        public string history_point { get; set; }
        public string adl_ranking { get; set; }
        public string pending_orders { get; set; }
        public CloseOrder close_order { get; set; }
    }

    public partial class CloseOrder
    {
        public string id { get; set; }
        public string price { get; set; }
        public string is_liq { get; set; }
    }
}
