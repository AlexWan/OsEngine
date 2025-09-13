using System.Collections.Generic;

namespace OsEngine.Market.Servers.XT.XTFutures.Entity
{
    public class XTFuturesRequestMessageWebSocketPublic
    {
        public string method { get; set; }           //SUBSCRIBE, UNSUBSCRIBE
        public List<string> @params { get; set; }     //{topic}@{arg},{arg}
        public string id { get; set; }
    }

    public class XTFuturesRequestMessageWebSocketPrivate : XTFuturesRequestMessageWebSocketPublic
    {
        public string listenKey { get; set; }        //private API token
    }
}