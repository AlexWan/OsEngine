/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMex.BitMexEntity
{
    public class DatumTrades
    {
        public string timestamp { get; set; }
        public string symbol { get; set; }
        public string side { get; set; }
        public string size { get; set; }
        public string price { get; set; }
        public string tickDirection { get; set; }
        public string trdMatchID { get; set; }
        public string grossValue { get; set; }
        public string homeNotional { get; set; }
        public string foreignNotional { get; set; }
        public string trdType { get; set; }
    }

    public class BitMexTrades
    {
        public string table { get; set; }
        public string action { get; set; }
        public List<DatumTrades> data { get; set; }
    }
}
