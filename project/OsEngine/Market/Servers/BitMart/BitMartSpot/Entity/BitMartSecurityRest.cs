/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMart.Json
{
    public class BitMartBaseMessage<T>
    {
        public string code;
        public string trace;
        public string message;
        public T data;
    }

    public class SecurityData
    {
        public List<BitMartSecurityRest> symbols { get; set; }
    }

    public class BitMartSecurityRest
    {
        public string symbol { get; set; }
        public string symbol_id { get; set; }
        public string base_currency { get; set; }
        public string quote_currency { get; set; }
        public string quote_increment { get; set; }
        public string base_min_size { get; set; }
        public string price_min_precision { get; set; }
        public string price_max_precision { get; set; }
        public string expiration { get; set; }
        public string min_buy_amount { get; set; }
        public string min_sell_amount { get; set; }
        public string trade_status { get; set; }

    }

    public class BitMartCandle
    {
        public string code { get; set; }
        public string trace { get; set; }
        public string message { get; set; }
        public List<List<string>> data { get; set; }
    }
}