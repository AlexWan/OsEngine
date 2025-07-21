/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMartFutures.Json
{
    public class BitMartBaseMessage<T>
    {
        public string code;
        public string trace;
        public string message;
        public T data;
    }

    public class BitMartSecurityRest
    {
        public string code { get; set; }
        public string message { get; set; }
        public string trace { get; set; }
        public SecurityData data { get; set; }
    }

    public class SecurityData
    {
        public List<BitMartSymbol> symbols { get; set; }
    }

    public class BitMartSymbol
    {
        public string symbol { get; set; }
        public string product_type { get; set; }
        public string open_timestamp { get; set; }
        public string expire_timestamp { get; set; }
        public string settle_timestamp { get; set; }
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
        public string market_max_volume { get; set; }
        public string min_volume { get; set; }
        public string funding_rate { get; set; }
        public string expected_funding_rate { get; set; }
        public string open_interest { get; set; }
        public string open_interest_value { get; set; }
        public string high_24h { get; set; }
        public string low_24h { get; set; }
        public string change_24h { get; set; }
        public string funding_interval_hours { get; set; }
        public string status { get; set; }
        public string delist_time { get; set; }
    }

    public class BitMartCandlesHistory
    {
        public string code { get; set; }
        public string trace { get; set; }
        public string message { get; set; }
        public List<BitMartCandle> data { get; set; }
    }

    public class BitMartCandle
    {
        public string timestamp;
        public string open_price;
        public string high_price;
        public string low_price;
        public string close_price;
        public string volume;
    }

    public class FundingItem
    {
        public List<FundingItemHistory> list { get; set; }
    }

    public class FundingItemHistory
    {
        public string symbol { get; set; }
        public string funding_rate { get; set; }
        public string funding_time { get; set; }
    }
}