/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Market.Servers.Alor.Json
{
    public class AlorPortfolioRest
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