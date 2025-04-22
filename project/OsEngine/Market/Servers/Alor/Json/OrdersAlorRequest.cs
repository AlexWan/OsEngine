/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Market.Servers.Alor.Json
{
    public class LimitOrderAlorRequest
    {
        public string side; // buy sell
        public string type; // limit market
        public int quantity;
        public decimal price;
        public instrumentAlor instrument;
        public string comment; // user ID
        public User user;
        public string timeInForce = "OneDay";
        public string icebergFixed;
        public string icebergVariance;
        public bool allowMargin = true; // allow marginal position
    }

    public class MarketOrderAlorRequest
    {
        public string side; // buy sell
        public string type; // limit market
        public int quantity;
        public instrumentAlor instrument;
        public string comment; // user ID
        public User user;
        public bool allowMargin = true; // allow marginal position
    }

    public class instrumentAlor
    {
        public string symbol;
        public string exchange = "MOEX";
    }

    public class User
    {
        public string portfolio;
    }
}
