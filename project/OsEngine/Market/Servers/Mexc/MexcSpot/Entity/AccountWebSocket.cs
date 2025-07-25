namespace OsEngine.Market.Servers.Mexc.MexcSpot.Entity
{
    public class AccountWebSocket
    {
        public string channel { get; set; }
        public MexcSocketBalance privateAccount { get; set; }
        public string symbol { get; set; }
        public string sendTime { get; set; }
    }

    public class MexcSocketBalance
    {
        public string vcoinName { get; set; }
        public string coinId { get; set; }
        public string balanceAmount { get; set; }
        public string balanceAmountChange { get; set; }
        public string frozenAmount { get; set; }
        public string frozenAmountChange { get; set; }
        public string type { get; set; }
        public string time { get; set; }
    }
}
