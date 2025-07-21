/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMartFutures.Json
{
    public class SoketBaseMessage<T>
    {
        public string group;

        public T data;
    }

    public class MarketTrade
    {
        public string trade_id { get; set; }
        public string symbol { get; set; }
        public string deal_price { get; set; }
        public string deal_vol { get; set; }
        public string created_at { get; set; }
        public string way { get; set; }
        public string m { get; set; }
    }

    public class MarketDepthBitMart
    {
        public string symbol { get; set; }

        /*
        Trading side
        -1=bid
        -2=ask
        */
        public string way { get; set; }

        public List<MarketDepthLevelBitMart> depths { get; set; }

        public string ms_t { get; set; }
    }

    public class MarketDepthLevelBitMart
    {
        public string price { get; set; }
        public string vol { get; set; }
    }

    public class FundingData
    {
        public string symbol { get; set; }
        public string fundingRate { get; set; }
        public string fundingTime { get; set; }
        public string nextFundingRate { get; set; }
        public string nextFundingTime { get; set; }
        public string funding_upper_limit { get; set; }
        public string funding_lower_limit { get; set; }
        public string ts { get; set; }
    }
}