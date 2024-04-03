/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Market.Servers.Alor.Json
{
    public class AlorSecurity
    {
        public string symbol { get; set; }
        public string shortname { get; set; }
        public string description { get; set; }
        public string exchange { get; set; }
        public string type { get; set; }
        public string lotsize { get; set; }
        public string facevalue { get; set; }
        public string cfiCode { get; set; }
        public string cancellation { get; set; }
        public string minstep { get; set; }
        public string rating { get; set; }
        public string marginbuy { get; set; }
        public string marginsell { get; set; }
        public string marginrate { get; set; }
        public string pricestep { get; set; }
        public string priceMax { get; set; }
        public string priceMin { get; set; }
        public string theorPrice { get; set; }
        public string theorPriceLimit { get; set; }
        public string volatility { get; set; }
        public string currency { get; set; }
        public string board { get; set; }
        public string primary_board { get; set; }
        public string tradingStatus { get; set; }
        public string tradingStatusInfo { get; set; }
        public string priceMultiplier { get; set; }
        public string complexProductCategory { get; set; }
    }
}