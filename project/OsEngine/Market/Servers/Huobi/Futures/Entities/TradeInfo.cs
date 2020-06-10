using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Huobi.Futures.Entities
{
    public class TradeData
    {
        public decimal amount { get; set; }
        public long ts { get; set; }
        public long id { get; set; }
        public decimal price { get; set; }
        public string direction { get; set; }
    }

    public class Tick
    {
        public long id { get; set; }
        public long ts { get; set; }
        public IList<TradeData> data { get; set; }
    }

    public class TradeInfo
    {
        public string ch { get; set; }
        public long ts { get; set; }
        public Tick tick { get; set; }
    }
}
