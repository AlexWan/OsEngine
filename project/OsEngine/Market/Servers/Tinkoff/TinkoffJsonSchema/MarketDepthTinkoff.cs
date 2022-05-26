using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Tinkoff.TinkoffJsonSchema
{
    public class MarketDepthTinkoffResponse
    {
        public string figi;

        public string depth;

        public List<MarketDepthLineTinkoff> asks;

        public List<MarketDepthLineTinkoff> bids;

        public Quotation lastPrice;

        public Quotation closePrice;

        public Quotation limitUp;

        public Quotation limitDown;

    }

    public class MarketDepthLineTinkoff
    {
        public Quotation price;

        public string quantity;
    }
}
