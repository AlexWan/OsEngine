/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Market.Servers.Alor.Json
{
    public class RequestSocketSubscribleTrades
    {
        public string token;
        public string guid;
        public string code;
        public string opcode = "AllTradesGetAndSubscribe";
        public int depth = 50;
        public string includeVirtualTrades = "false";
        public string exchange = "MOEX";
        public string format = "Simple";
        public string frequency = "0";
       
    }

    public class RequestSocketSubscribleMarketDepth
    {
        public string guid;
        public string token;
        public string code;
        public string opcode = "OrderBookGetAndSubscribe";
        public string depth = "10";
        public string exchange = "MOEX";
        public string format = "Simple";
        public string frequency = "0";
    }

    public class RequestSocketSubscriblePoftfolio
    {
        public string guid;
        public string token;
        public string portfolio;
        public string opcode = "SummariesGetAndSubscribeV2";
        public string exchange = "MOEX";
        public string format = "Simple";
        public string frequency= "0";
    }

    public class RequestSocketSubscriblePositions
    {
        public string guid;
        public string token;
        public string portfolio;
        public string opcode = "PositionsGetAndSubscribeV2";
        public string exchange = "MOEX";
        public string format = "Simple";
        public string frequency = "0";
    }

    public class RequestSocketSubscribleOrders
    {
        public string guid;
        public string token;
        public string portfolio;
        public string opcode = "OrdersGetAndSubscribeV2";
        public string [] orderStatuses = new string[] { "filled", "working", "canceled", "rejected" };
        public string exchange = "MOEX";
        public string format = "Simple";
        public string frequency = "0";
    }

    public class RequestSocketSubscribleMyTrades
    {
        public string guid;
        public string token;
        public string portfolio;
        public string opcode = "TradesGetAndSubscribeV2";
        public string exchange = "MOEX";
        public string format = "Simple";
        public string frequency="0";
    }
}
