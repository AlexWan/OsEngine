using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.OKX.Entity
{
    public class OrderRequest<T>
    {
        public string id;
        public string op = "order";
        public List<T> args = new List<T>();
    }

    public class OrderRequestArgsSwap
    {
        public string side;
        public string posSide;
        public string instId;
        public string tdMode;
        public string ordType;
        public string sz;
        public string px;
        public string clOrdId;
        public bool reduceOnly;
    }

    public class OrderRequestArgsSpot
    {
        public string side;
        public string instId;
        public string tdMode;
        public string ordType;
        public string sz;
        public string clOrdId;
    }
}
