using System;


namespace OsEngine.Market.Servers.MoexFixFastCurrency.Entity
{
    public class FastConnection
    {
        public string FeedType { get; set; }
        public string FeedID { get; set; }
        public string MulticastIP { get; set; }
        public string SrsIP { get; set; }
        public int Port { get; set; }
    }
}

