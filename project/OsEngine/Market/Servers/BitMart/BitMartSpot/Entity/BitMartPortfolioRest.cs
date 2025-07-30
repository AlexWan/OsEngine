/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMart.Json
{
    public class PortfolioData
    {
        public List<BitMartSpotPortfolioItem> wallet { get; set; }
    }

    public class BitMartSpotPortfolioItem
    {
        public string id { get; set; }
        public string available { get; set; }
        public string name { get; set; }
        public string frozen { get; set; }
        public string total { get; set; }
    }
}