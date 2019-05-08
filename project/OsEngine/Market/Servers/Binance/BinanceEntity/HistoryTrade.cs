using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Binance.BinanceEntity
{
    public class HistoryTrade
    {
        public string id;
        public string price;
        public string qty;
        public string quoteQty;
        public string time;
        public string isBuyerMaker;
        public string isBuyerMatch;
    }
}
