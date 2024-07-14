/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using System.Windows.Documents;

namespace OsEngine.Market.Servers.BitMart.Json
{

    public class BitMartSpotPortfolioItems : List<BitMartSpotPortfolioItem> 
    { }

    public class BitMartSpotPortfolioItem
    {
        public string id { get; set; }
        public string available { get; set; }
        public string name { get; set; }
        public string frozen { get; set; }

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