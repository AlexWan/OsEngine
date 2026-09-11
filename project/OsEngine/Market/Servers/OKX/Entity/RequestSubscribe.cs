using System.Collections.Generic;
using Newtonsoft.Json;

namespace OsEngine.Market.Servers.OKX.Entity
{

    public class RequestSubscribe<T>
    {
        public string op { get; set; } = "subscribe";
        public List<T> args { get; set; }
    }

    public class SubscribeArgs
    {
        public string channel { get; set; }
        public string instId { get; set; }

        // required for the option-trades channel, ignored for the rest
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string instType { get; set; }
    }

    public class SubscribeArgsAccount
    {
        public string channel { get; set; }
        public string instType { get; set; }
    }

    public class SubscribeArgsOption
    {
        public string channel { get; set; }
        public string instFamily { get; set; }
    }
}
