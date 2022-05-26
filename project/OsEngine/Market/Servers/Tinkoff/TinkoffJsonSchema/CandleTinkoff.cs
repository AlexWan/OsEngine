using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Tinkoff.TinkoffJsonSchema
{
    public class CandlesResponse
    {
       public List<CandleTinkoff> candles;
    }

    public class CandleTinkoff
    {
        public Quotation open;

        public Quotation high;

        public Quotation low;

        public Quotation close;

        public string volume;

        public string time;

        public string isComplete;

    }
}
