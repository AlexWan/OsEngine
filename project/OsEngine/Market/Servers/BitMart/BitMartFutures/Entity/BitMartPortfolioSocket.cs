/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMartFutures.Json
{
    public class BitMartBalanceDetail
    {
        public string currency { get; set; }
        public string available_balance { get; set; }
        public string position_deposit { get; set; }
        public string frozen_balance { get; set; }
    }

    public class BitMartPosition
    {
        public string symbol { get; set; }
        public string hold_volume { get; set; }
        public int position_type { get; set; }
        public int open_type { get; set; }
        public string frozen_volume { get; set; }
        public string close_volume { get; set; }
        public string hold_avg_price { get; set; }
        public string close_avg_price { get; set; }
        public string open_avg_price { get; set; }
        public string liquidate_price { get; set; }
        public long create_time { get; set; }
        public long update_time { get; set; }
    }

    public class BitMartPositions : List<BitMartPosition>
    {

    }
}
