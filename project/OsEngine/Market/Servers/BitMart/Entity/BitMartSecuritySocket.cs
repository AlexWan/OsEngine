/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Jayrock.Json;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMart.Json
{
    public class SoketBaseMessage
    { 
       public string table;

       public object data;
    }

    public class MarketQuotesMessage
    {
        public string table;

        public List<QuotesBitMart> data;
    }

    public class QuotesBitMart
    {
        public string symbol;
        public string price;
        public string side;
        public string size;
        public ulong s_t;
    }

    public class MarketDepthFullMessage
    {
        public string table;

        public List<MarketDepthBitMart> data;

    }

    public class MarketDepthBitMart
    {
        public List<MarketDepthLevelBitMart> asks;

        public List<MarketDepthLevelBitMart> bids;

        public string symbol;

        public ulong ms_t;
    }

    public class MarketDepthLevelBitMart : List<string>
    {
    }

}