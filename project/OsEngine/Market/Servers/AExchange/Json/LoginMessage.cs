using Newtonsoft.Json;
using OsEngine.Market.Servers.AE.Json;

namespace OsEngine.Market.Servers.AE.Json
{
    public class WebSocketLoginMessage : WebSocketMessageBase
    {
        [JsonProperty("login")]
        public string Login { get; set; }

        public WebSocketLoginMessage()
        {
            Type = "Login";
        }
    }
}
