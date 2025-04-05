/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.Mexc.Json
{

    public class MexcTrade
    {
        public string symbol { get; set; }
        public string id { get; set; }
        public string orderId { get; set; }
        public string orderListId { get; set; }
        public string price { get; set; }
        public string qty { get; set; }
        public string quoteQty { get; set; }
        public string commission { get; set; }
        public string commissionAsset { get; set; }
        public string time { get; set; }
        public string isBuyer { get; set; }
        public string isMaker { get; set; }
        public string isBestMatch { get; set; }
        public string isSelfTrade { get; set; }
        public string clientOrderId { get; set; }
    }

    public class MexcTrades : List<MexcTrade>
    {

    }

    public class MexcOrder
    {
        public string symbol { get; set; }
        public string side { get; set; }
        public string type { get; set; }
        public string notional { get; set; }
        public string size { get; set; }
        public string ms_t { get; set; }
        public string price { get; set; }
        public string filled_notional { get; set; }
        public string filled_size { get; set; }
        public string margin_trading { get; set; }
        public string state { get; set; }
        public string order_id { get; set; }
        public string order_type { get; set; }
        public string last_fill_time { get; set; }
        public string last_fill_price { get; set; }
        public string last_fill_count { get; set; }
        public string exec_type { get; set; }
        public string detail_id { get; set; }
        public string client_order_id { get; set; }
        public string create_time { get; set; }
        public string update_time { get; set; }
        public string order_mode { get; set; }
        public string entrust_type { get; set; }
        public string order_state { get; set; }
    }

    public class MexcOrders : List<MexcOrder>
    {

    }

    public class MexcRestOrder
    {
        public string orderId { get; set; }
        public string clientOrderId { get; set; }
        public string symbol { get; set; }
        public string side { get; set; }
        public string orderMode { get; set; }
        public string type { get; set; }
        public string state { get; set; }
        public string price { get; set; }
        public string priceAvg { get; set; }
        public string size { get; set; }
        public string filledSize { get; set; }
        public string notional { get; set; }
        public string filledNotional { get; set; }
        public string createTime { get; set; }
        public string updateTime { get; set; }
    }

    public class MexcRestOrders : List<MexcRestOrder>
    {

    }

    public class MexcRestOrdersBaseMessage
    {
        public string code;
        public string trace;
        public string message;
        public string data;
    }

    public class MexcNewOrderResponse
    {
        public string symbol { get; set; }
        public string orderId { get; set; }
        public string orderListId { get; set; }
        public string price { get; set; }
        public string origQty { get; set; }
        public string type { get; set; }
        public string side { get; set; }
        public string transactTime { get; set; }
    }

    public class MexcOrderResponse
    {
        public string symbol { get; set; }
        public string origClientOrderId { get; set; }
        public string orderId { get; set; }
        public string clientOrderId { get; set; }
        public string price { get; set; }
        public string origQty { get; set; }
        public string executedQty { get; set; }
        public string cummulativeQuoteQty { get; set; }
        public string status { get; set; }
        public string timeInForce { get; set; }
        public string type { get; set; }
        public string side { get; set; }
        public string stopPrice { get; set; }
        public string icebergQty { get; set; }
        public string time { get; set; }
        public string updateTime { get; set; }
        public string isWorking { get; set; }
        public string origQuoteOrderQty { get; set; }

    }

    public class MexcOrderListResponse : List<MexcOrderResponse>
    {

    }

    public class MexcDefaultSymbols
    {
        public string code { get; set; }
        public List<string> data { get; set; }
        public string msg { get; set; }
    }
}
