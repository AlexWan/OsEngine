/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMart.Json
{

    public class NewOrderBitMartRequest
    {
        public string symbol;
        public string side; // buy sell
        public string type; // limit market
        public string size;
        public string price;
        public string client_order_id; // user ID
    }

    public class CancelOrderBitMartRequest
    {
        public string symbol;
        public string client_order_id; // exchange order ID
    }

    public class GetTradesBitMartRequest
    {
        public string orderId;
        public long recvWindow; // default 5000 milliseconds
    }

    public class BitMartTrade
    {
        public string tradeId { get; set; }
        public string orderId { get; set; }
        public string clientOrderId { get; set; }
        public string symbol { get; set; }
        public string side { get; set; }
        public string orderMode { get; set; }
        public string type { get; set; }
        public string price { get; set; }
        public string size { get; set; }
        public string notional { get; set; }
        public string fee { get; set; }
        public string feeCoinName { get; set; }
        public string tradeRole { get; set; }
        public long createTime { get; set; }
        public long updateTime { get; set; }
    }

    public class BitMartTrades : List<BitMartTrade>
    {

    }


    public class BitMartOrder
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

    public class BitMartOrders : List<BitMartOrder>
    {

    }

    public class BitMartRestOrder
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
        public long createTime { get; set; }
        public long updateTime { get; set; }
    }

    public class BitMartRestOrders : List<BitMartRestOrder>
    {

    }

    public class BitMartRestOrdersBaseMessage
    {
        public int code;
        public string trace;
        public string message;
        public object data;
    }

}
