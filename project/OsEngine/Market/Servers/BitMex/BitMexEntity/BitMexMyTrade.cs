/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMex.BitMexEntity
{
    public class DatumMyTrade
    {
        public string execID { get; set; } // trade number in the system
        public string orderID { get; set; } // parent's order number
        public string price { get; set; } // trade price
        public string symbol { get; set; } // instrument
        public string side { get; set; } // trade side 
        public string orderQty { get; set; } // trade volume
        public string transactTime { get; set; } // trade time
        public string execType { get; set; } // trade state
        public string ordStatus { get; set; } // parent's order state
        public string clOrdID { get; set; } // order number in the robot
        public string avgPx { get; set; } // real execution price
        public string lastQty { get; set; }
    }

    public class BitMexMyTrade
    {
        public string table { get; set; }
        public string action { get; set; }
        public List<DatumMyTrade> data { get; set; }
    }
}
