using System.Collections.Generic;

namespace OsEngine.Market.Servers.OKXData.Entity
{
    public class OkxCandlesResponce
    {
        public string code;
        public string msg;
        public List<List<string>> data;
    }
}
