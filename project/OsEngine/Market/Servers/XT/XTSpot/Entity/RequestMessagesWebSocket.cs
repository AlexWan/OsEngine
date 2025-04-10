using System.Collections.Generic;

namespace OsEngine.Market.Servers.XT.XTSpot.Entity
{
    public class RequestMessageWebSocketPublic
    {
        public string method { get; set; }           //SUBSCRIBE, UNSUBSCRIBE
        public List<string> @params { get; set; }     //{topic}@{arg},{arg}
        public string id { get; set; }
    }

    public class RequestMessageWebSocketPrivate : RequestMessageWebSocketPublic
    {
        public string listenKey { get; set; }        //private API token
    }
}