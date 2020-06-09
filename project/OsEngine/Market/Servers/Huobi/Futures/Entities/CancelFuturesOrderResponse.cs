using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Huobi.Futures.Entities
{
    public class OrderCancelData
    {
        public IList<object> errors { get; set; }
        public string successes { get; set; }
    }

    public class CancelFuturesOrderResponse
    {
        public string status { get; set; }
        public OrderCancelData data { get; set; }
        public long ts { get; set; }
    }
}
