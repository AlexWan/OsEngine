using System.Collections.Generic;
using Newtonsoft.Json;

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

        // required for the option-trades channel, ignored for the rest
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string instType;
    }

    public class SubscribeArgsAccount
    {
        public string channel;
        public string instType;
    }

    public class SubscribeArgsOption
    {
        public string channel;
        public string instFamily;
    }

}
