/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMartFutures.Json
{

    public class BitMartFuturesPortfolioItems : List<BitMartFuturesPortfolioItem>
    { }

    public class BitMartFuturesPortfolioItem
    {
        public string currency { get; set; }
        public string position_deposit { get; set; }
        public string frozen_balance { get; set; }
        public string available_balance { get; set; }
        public string equity { get; set; }
        public string unrealized { get; set; }

    }

    public class BitMartPortfolioRest
    {
        public string buyingPowerAtMorning { get; set; }
        public string buyingPower { get; set; }
        public string profit { get; set; }
        public string profitRate { get; set; }
        public string portfolioEvaluation { get; set; }
        public string portfolioLiquidationValue { get; set; }
        public string initialMargin { get; set; }
        public string riskBeforeForcePositionClosing { get; set; }
    }

}