using System.Collections.Generic;

namespace OsEngine.Market.Servers.ExMo.ExmoSpot.Entity
{
    public class ResponseRestMessage<T>
    {
    }

    public class SymbolItem
    {
        public string min_quantity { get; set; }
        public string max_quantity { get; set; }
        public string min_price { get; set; }
        public string max_price { get; set; }
        public string max_amount { get; set; }
        public string min_amount { get; set; }
        public string price_precision { get; set; }
        public string commission_taker_percent { get; set; }
        public string commission_maker_percent { get; set; }
    }

    public class MarketInfo : Dictionary<string, SymbolItem>
    {
    }

    public class BalanceResponse
    {
        public string uid { get; set; }
        public string server_date { get; set; }
        public Dictionary<string, string> balances { get; set; }
        public Dictionary<string, string> reserved { get; set; }
        public Dictionary<string, string> staked { get; set; }
    }

    public class CandleItem
    {
        public string t { get; set; }
        public string o { get; set; }
        public string c { get; set; }
        public string h { get; set; }
        public string l { get; set; }
        public string v { get; set; }
    }

    public class CandlesResponse
    {
        public string s { get; set; }
        public string errmsg { get; set; }
        public List<CandleItem> candles { get; set; }
    }

    public class SendOrderResponse
    {
        public string result { get; set; }
        public string error { get; set; }
        public string order_id { get; set; }
        public string client_id { get; set; }
    }
}
