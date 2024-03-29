using System.Collections.Generic;

namespace OsEngine.Market.Servers.MoexAlgopack.Entity
{
    public class ResponseTrades
    {
        public Trades trades { get; set; }
    }

    public class Trades
    {
        public List<string> columns { get; set; }
        public List<List<string>> data { get; set; }
    }
}
