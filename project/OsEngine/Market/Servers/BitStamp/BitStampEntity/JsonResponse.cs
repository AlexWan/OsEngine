/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitStamp.BitStampEntity
{

    public class TradeResponse
    {
        public string id { get; set; }
        public string amount { get; set; }
        public string price { get; set; }
        public string type { get; set; }
        public string timestamp { get; set; }
        public string buy_order_id { get; set; }
        public string sell_order_id { get; set; }
    }

    public class PairInfoResponse
    {
        public string name { get; set; }
        public string url_symbol { get; set; }
        public string base_decimals { get; set; }
        public string counter_decimals { get; set; }
        public string minimum_order { get; set; }
        public string trading { get; set; }
        public string description { get; set; }

    }

    public class OrderStatusResponse
    {
        public string status { get; set; }
        public Transaction[] transactions { get; set; }
    }

    public class Transaction
    {
        public string fee { get; set; }
        public string price { get; set; }
        public string datetime { get; set; }
        public string usd { get; set; }
        public string btc { get; set; }
        public string tid { get; set; }
        public string type { get; set; }
    }

    public class OrderBookResponse
    {
        public List<List<string>> bids { get; set; }
        public List<List<string>> asks { get; set; }
    }

    public class BalanceResponse
    {
        public string usd_balance { get; set; }
        public string btc_balance { get; set; }
        public string eur_balance { get; set; }
        public string usd_reserved { get; set; }
        public string btc_reserved { get; set; }
        public string eur_reserved { get; set; }
        public string usd_available { get; set; }
        public string btc_available { get; set; }
        public string eur_available { get; set; }
        public string fee { get; set; }
    }

    public class BuySellResponse
    {
        public string id { get; set; }
        public string datetime { get; set; }
        public string type { get; set; }
        public string price { get; set; }
        public string amount { get; set; }
        public string status { get; set; }
        public string reason { get; set; }
    }

    public class BitstampData
    {
        public string data;

        public string Event;

        public string channel;

        public string security;
    }
}
