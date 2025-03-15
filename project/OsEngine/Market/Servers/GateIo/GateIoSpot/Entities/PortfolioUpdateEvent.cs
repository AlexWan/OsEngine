using System.Collections.Generic;

namespace OsEngine.Market.Servers.GateIo.GateIoSpot.Entities
{
    public class CurrencyBalance
    {
        public string Timestamp { get; set; }
        public string TimestampMs { get; set; }
        public string User { get; set; }
        public string Currency { get; set; }
        public string Change { get; set; }
        public string Total { get; set; }
        public string Available { get; set; }
        public string Freeze { get; set; }
        public string FreezeChange { get; set; }
        public string ChangeType { get; set; }
    }

    public class PortfolioUpdateEvent
    {
        public int Time { get; set; }
        public long TimeMs { get; set; }
        public string Channel { get; set; }
        public string Event { get; set; }
        public List<CurrencyBalance> Balances { get; set; }
    }

}
