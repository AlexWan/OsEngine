using Newtonsoft.Json;

namespace OsEngine.Market.Servers.TraderNet.Entity
{
    public class RequestCandle
    {
        public Q q { get; set; }

        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class Q
        {
            public string cmd { get; set; }
            public Params @params { get; set; }

            public string SID;
        }

        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class Params
        {
            public string id;
            public int userId;
            public int timeframe;
            public int count;
            public string date_from;
            public string date_to;
            public string intervalMode = "ClosedRay";
            public string apiKey;

        }
    }
}
