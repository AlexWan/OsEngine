/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMartFutures.Json
{
    public class SoketBaseMessage
    {
        public string group;

        public object data;
    }

    public class MarketTrades : List<MarketTrade>
    {

    }

    public class MarketTrade
    {
        public ulong trade_id { get; set; }
        public string symbol { get; set; }
        public string deal_price { get; set; }
        public string deal_vol { get; set; }
        public string created_at { get; set; }
        public int way { get; set; }
        public int type { get; set; }
    }


    public class MarketDepthBitMart
    {
        public string symbol { get; set; }

        /*
        Trading side
        -1=bid
        -2=ask
        */
        public long way { get; set; }

        public List<MarketDepthLevelBitMart> depths { get; set; }

        public ulong ms_t { get; set; }
    }

    public class MarketDepthLevelBitMart
    {
        public string price { get; set; }
        public string vol { get; set; }
    }

}