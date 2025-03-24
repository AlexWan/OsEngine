
namespace OsEngine.Market.Servers.Alor.Json
{
    public class RequestSocketSubscribeTrades
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

    public class RequestSocketSubscribeMarketDepth
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

    public class RequestSocketSubscribePortfolio
    {
        public string guid;
        public string token;
        public string portfolio;
        public string opcode = "SummariesGetAndSubscribeV2";
        public string exchange = "MOEX";
        public string format = "Simple";
        public string frequency = "0";
    }

    public class RequestSocketSubscribePositions
    {
        public string guid;
        public string token;
        public string portfolio;
        public string opcode = "PositionsGetAndSubscribeV2";
        public string exchange = "MOEX";
        public string format = "Simple";
        public string frequency = "0";
    }

    public class RequestSocketSubscribeOrders
    {
        public string guid;
        public string token;
        public string portfolio;
        public string opcode = "OrdersGetAndSubscribeV2";
        public string[] orderStatuses = new string[] { "filled", "working", "canceled", "rejected" };
        public string exchange = "MOEX";
        public string format = "Simple";
        public string frequency = "0";
    }

    public class RequestSocketSubscribeMyTrades
    {
        public string guid;
        public string token;
        public string portfolio;
        public string opcode = "TradesGetAndSubscribeV2";
        public string exchange = "MOEX";
        public string format = "Simple";
        public string frequency = "0";
    }
}
