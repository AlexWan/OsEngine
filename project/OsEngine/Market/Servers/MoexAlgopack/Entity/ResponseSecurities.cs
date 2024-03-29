using System.Collections.Generic;

namespace OsEngine.Market.Servers.MoexAlgopack.Entity
{
    public class ResponseSecurities
    {
        public Securities securities { get; set; }
    }

    public class Securities
    {
        public List<string> columns { get; set; }
        public List<List<string>> data { get; set; }
    }
}
