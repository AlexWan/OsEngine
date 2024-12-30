using System.Collections.Generic;


namespace OsEngine.Market.Servers.OKX.Entity
{
    public class CandlesResponse
    {
        public string code;
        public string msg;
        public List<List<string>> data;
    }
}
