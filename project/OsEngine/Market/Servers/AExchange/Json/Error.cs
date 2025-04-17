using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;

namespace OsEngine.Market.Servers.AE.Json
{
    public class WebSocketErrorMessage : WebSocketMessageBase
    {
        [JsonProperty("reqid")]
        public int RequestId { get; set; }

        [JsonProperty("msg")]
        public string Message { get; set; }

        [JsonProperty("code", NullValueHandling = NullValueHandling.Ignore)]
        public int? Code { get; set; }

    }
}
