/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.Mexc.Json
{
    public class SoketBaseMessage
    {
        public string c { get; set; } //channel
        public object d { get; set; } //data
        public string s { get; set; } //symbol
        public ulong t { get; set; }  //time
    }

    public class MexcDeals
    {
        public List<MexcDeal> deals { get; set; }
        public string e { get; set; }
    }

    public class MexcDeal
    {
        public int S { get; set; }
        public string p { get; set; }
        public long t { get; set; }
        public string v { get; set; }
    }

    public class MexcDepthRow
    {
        public string p { get; set; }
        public string v { get; set; }
    }

    public class MexcDepth
    {
        public List<MexcDepthRow> asks { get; set; }
        public List<MexcDepthRow> bids { get; set; }
        public string e { get; set; }
        public string r { get; set; }
    }
}