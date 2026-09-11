using System.Collections.Generic;


namespace OsEngine.Market.Servers.OKX.Entity
{
    public class CandlesResponse
    {
        public string code { get; set; }
        public string msg { get; set; }
        public List<List<string>> data { get; set; }
    }
}
