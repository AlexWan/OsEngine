﻿/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.Mexc.Json
{

    public class MexcBalance
    {
        public string asset { get; set; }
        public string free { get; set; }
        public string locked { get; set; }
    }

    public class MexcPortfolioRest
    {
        public bool canTrade { get; set; }
        public bool canWithdraw { get; set; }
        public bool canDeposit { get; set; }
        public object updateTime { get; set; }
        public string accountType { get; set; }
        public List<MexcBalance> balances { get; set; }
        public List<string> permissions { get; set; }
    }

}