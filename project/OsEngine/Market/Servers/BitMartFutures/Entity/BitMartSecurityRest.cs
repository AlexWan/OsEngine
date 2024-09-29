/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMartFutures.Json
{

    public class BitMartBaseMessage
    {
        public int code;
        public string trace;
        public string message;
        public object data;
    }

    public class BitMartBaseMessageDict
    {
        public int code;
        public string trace;
        public string message;
        public Dictionary<string, object> data;
    }

    public class BitMartSecurityRest
    {
        public string symbol { get; set; }
        public int product_type { get; set; }
        public ulong open_timestamp { get; set; }
        public ulong expire_timestamp { get; set; }
        public ulong settle_timestamp { get; set; }
        public string base_currency { get; set; }
        public string quote_currency { get; set; }
        public string last_price { get; set; }
        public string volume_24h { get; set; }
        public string turnover_24h { get; set; }
        public string index_price { get; set; }
        public string index_name { get; set; }
        public string contract_size { get; set; }
        public string min_leverage { get; set; }
        public string max_leverage { get; set; }
        public string price_precision { get; set; }
        public string vol_precision { get; set; }
        public string max_volume { get; set; }
        public string min_volume { get; set; }
        public string funding_rate { get; set; }
        public string expected_funding_rate { get; set; }
        public string open_interest { get; set; }
        public string open_interest_value { get; set; }
        public string high_24h { get; set; }
        public string low_24h { get; set; }
        public string change_24h { get; set; }

    }

    public class BitMartCandlesHistory: List<BitMartCandle>
    {

    }

    public class BitMartCandle
    {
        public ulong timestamp;
        public string open_price;
        public string high_price;
        public string low_price;
        public string close_price;
        public string volume;
    }
}