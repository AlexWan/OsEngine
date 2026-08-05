using Newtonsoft.Json;

namespace OsEngine.Market.Servers.TraderNet.Entity
{
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]

    public class RequestCandle
    {
        public string id;
        public int timeframe;
        public int count;
        public string date_from;
        public string date_to;
        public string intervalMode = "ClosedRay";
    }
}
