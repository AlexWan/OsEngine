using System.Collections.Generic;

namespace OsEngine.Market.Servers.OKX.Entity
{

    public class RequestSubscribe<T>
    {
        public string op = "subscribe";
        public List<T> args;
    }

    public class SubscribeArgs
    {
        public string channel;
        public string instId;
    }

    public class SubscribeArgsAccount
    {
        public string channel;
        public string instType;
    }

}
