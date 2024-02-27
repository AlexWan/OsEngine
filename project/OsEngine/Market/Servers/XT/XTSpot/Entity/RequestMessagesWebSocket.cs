using System.Collections.Generic;
using Newtonsoft.Json;

namespace OsEngine.Market.Servers.XT.XTSpot.Entity
{
    public class RequestMessageWebSocketPublic 
    {
        [JsonProperty("method")]
        public string Method { get; set; }           //SUBSCRIBE, UNSUBSCRIBE
        [JsonProperty("params")]
        public List<string> Params { get; set; }     //{topic}@{arg},{arg}
        [JsonProperty("id")]
        public string Id { get; set; }               
    }

    public class RequestMessageWebSocketPrivate : RequestMessageWebSocketPublic
    {
        [JsonProperty("listenKey")]
        public string ListenKey { get; set; }        //private API token
    }
}