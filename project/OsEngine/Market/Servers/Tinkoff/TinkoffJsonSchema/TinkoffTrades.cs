using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Tinkoff.TinkoffJsonSchema
{
    public class TinkoffTrades
    {
        public string figi;
        public string direction;
        public Quotation price;
        public string quantity;
        public string time;
        public string instrumentUid;
    }


    //public class PriceTinkoff
    //{
    //    public string units;
    //    public string nano;
    //}

}