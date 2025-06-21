

namespace OsEngine.Market.Servers.GateIo.GateIoFutures.Entities
{
    public class GfContractStat
    {
        public string time { get; set; }
        public string time_ms { get; set; }
        public string channel { get; set; }
        public string @event { get; set; }
        public ContractStat result { get; set; }
    }

    public class ContractStat
    {
        public string time { get; set; }
        public string contract { get; set; }
        public string lsr_taker { get; set; }
        public string lsr_account { get; set; }
        public string long_liq_size { get; set; }
        public string short_liq_size { get; set; }
        public string open_interest { get; set; }
        public string short_liq_usd { get; set; }
        public string mark_price { get; set; }
        public string top_lsr_size { get; set; }
        public string short_liq_amount { get; set; }
        public string long_liq_amount { get; set; }
        public string open_interest_usd { get; set; }
        public string top_lsr_account { get; set; }
        public string long_liq_usd { get; set; }
    }
}
