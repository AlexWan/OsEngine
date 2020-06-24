using System.Collections.Generic;

namespace OsEngine.Market.Servers.Huobi.Futures.Entities
{
    public class TradeNotify
    {
        public string id { get; set; }
        public decimal trade_id { get; set; }
        public decimal trade_volume { get; set; }
        public decimal trade_price { get; set; }
        public decimal trade_fee { get; set; }
        public decimal trade_turnover { get; set; }
        public long created_at { get; set; }
        public string role { get; set; }
    }

    public class OrderFuturesNotify
    {
        public string op { get; set; }
        public string topic { get; set; }
        public long ts { get; set; }
        public string symbol { get; set; }
        public string contract_type { get; set; }
        public string contract_code { get; set; }
        public decimal volume { get; set; }
        public decimal price { get; set; }
        public string order_price_type { get; set; }
        public string direction { get; set; }
        public string offset { get; set; }
        public int status { get; set; }
        public decimal lever_rate { get; set; }
        public long order_id { get; set; }
        public string order_id_str { get; set; }
        public int? client_order_id { get; set; }
        public string order_source { get; set; }
        public int order_type { get; set; }
        public long created_at { get; set; }
        public decimal trade_volume { get; set; }
        public decimal trade_turnover { get; set; }
        public decimal fee { get; set; }
        public decimal trade_avg_price { get; set; }
        public decimal margin_frozen { get; set; }
        public decimal profit { get; set; }
        public string fee_asset { get; set; }
        public IList<TradeNotify> trade { get; set; }
    }
}
