using Newtonsoft.Json;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.Polygon.Entity
{
    public class RestResponceMessage<T>
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]

        public string status;
        public string next_url;
        public List<T> results;
        public string message;
    }

    public class ResponceTrades
    {
        public string id;
        public string participant_timestamp;
        public string price;
        public string size;
        public string sequence_number;
        public string sip_timestamp;
        public string exchange;
        public string tape;
        public List<string> conditions;
    }

    public class ResponceCandles
    {
        public string v;
        public string t;
        public string o;
        public string h;
        public string l;
        public string c;
    }

    public class Tickers
    {
        public string ticker;
        public string name;
        public string type;
        public string primary_exchange;
    }

    public class TickerType
    {
        public string code;
        public string description;
    }

    public class TickerExchange
    {
        public string mic;
        public string name;
    }
}
