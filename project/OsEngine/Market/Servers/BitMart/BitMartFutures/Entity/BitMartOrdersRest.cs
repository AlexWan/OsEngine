/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMartFutures.Json
{

    public class NewOrderBitMartRequest
    {
        public string symbol { get; set; }
        public string client_order_id { get; set; }
        public int side { get; set; }
        public int mode { get; set; }
        public string type { get; set; }
        //public string leverage { get; set; }
        public string open_type { get; set; }
        public int size { get; set; }
        public string price { get; set; }

    }

    public class NewOrderBitMartResponce
    {
        public string order_id { get; set; }
        public string price { get; set; }
    }

    public class CancelOrderBitMartRequest
    {
        public string symbol;
        public string order_id; // exchange order ID
    }

    public class GetTradesBitMartRequest
    {
        public string orderId;
        public long recvWindow; // default 5000 milliseconds
    }

    public class BitMartTrade
    {
        public string order_id { get; set; }
        public string trade_id { get; set; }
        public string symbol { get; set; }
        public int side { get; set; }
        public string price { get; set; }
        public string vol { get; set; }
        public string exec_type { get; set; }
        public string profit { get; set; }
        public string realised_profit { get; set; }
        public string paid_fees { get; set; }
        public string create_time { get; set; }
    }

    public class BitMartTrades : List<BitMartTrade>
    {

    }


    public class BitMartRestOrder
    {
        public string order_id { get; set; }
        public string client_order_id { get; set; }
        public string price { get; set; }
        public string size { get; set; }
        public string symbol { get; set; }
        public int state { get; set; }
        public int side { get; set; }
        public string type { get; set; }
        public string leverage { get; set; }
        public string open_type { get; set; }
        public string deal_avg_price { get; set; }
        public string deal_size { get; set; }
        public string create_time { get; set; }
        public string update_time { get; set; }
    }

    public class BitMartRestOrders : List<BitMartRestOrder>
    {

    }

}
