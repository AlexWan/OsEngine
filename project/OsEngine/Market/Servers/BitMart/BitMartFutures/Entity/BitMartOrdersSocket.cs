/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Market.Servers.BitMartFutures.Json
{
    public class LastTrade
    {
        public string lastTradeID { get; set; }
        public string fillQty { get; set; }
        public string fillPrice { get; set; }
        public string fee { get; set; }
        public string feeCcy { get; set; }
    }

    public class BitMartOrder
    {
        public string order_id { get; set; }
        public string client_order_id { get; set; }
        public string price { get; set; }
        public string size { get; set; }
        public string symbol { get; set; }
        public string state { get; set; }
        public string side { get; set; }
        public string type { get; set; }
        public string leverage { get; set; }
        public string open_type { get; set; }
        public string deal_avg_price { get; set; }
        public string deal_size { get; set; }
        public string create_time { get; set; }
        public string update_time { get; set; }
        public string plan_order_id { get; set; }
        public LastTrade last_trade { get; set; }
        public string trigger_price { get; set; }
        public string trigger_price_type { get; set; }
        public string execution_price { get; set; }
        public string activation_price_type { get; set; }
        public string activation_price { get; set; }
        public string callback_rate { get; set; }
        public string position_mode { get; set; }
    }

    public class BitMartOrderAction
    {
        public string action { get; set; }
        public BitMartOrder order { get; set; }
    }
}
