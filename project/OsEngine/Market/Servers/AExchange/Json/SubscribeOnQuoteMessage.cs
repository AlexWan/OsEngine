using Newtonsoft.Json;
using OsEngine.Market.Servers.AE.Json;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.AE.Json
{
    public class WebSocketSubscribeOnQuoteMessage : WebSocketMessageBase
    {
        [JsonProperty("tickers")] public List<string> Tickers { get; set; }

        public WebSocketSubscribeOnQuoteMessage()
        {
            Type = "SubscribeOnQuote";
        }
    }
}
