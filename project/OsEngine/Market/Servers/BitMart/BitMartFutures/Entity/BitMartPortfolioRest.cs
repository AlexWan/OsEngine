/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMartFutures.Json
{
    public class BitMartPortfolioRest
    {
        public string code { get; set; }
        public string message { get; set; }
        public List<BalanceData> data { get; set; }
        public string trace { get; set; }
    }

    public class BalanceData
    {
        public string currency { get; set; }
        public string position_deposit { get; set; }
        public string frozen_balance { get; set; }
        public string available_balance { get; set; }
        public string equity { get; set; }
        public string unrealized { get; set; }
    }
}