using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.GateIo.Futures.Entities
{
    class WsRequestBuilder
    {
        private string _timeStamp;
        private GateFuturesWsReuest _wsReuest = new GateFuturesWsReuest();
        private Auth _auth;
        private Signer _signer;
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="request">The initial object</param>
        public WsRequestBuilder(long timestamp, string channel, string iEvent, string[] payload)
        {
            _wsReuest.Time = Convert.ToInt64(timestamp);
            _wsReuest.Channel = channel;
            _wsReuest.Event = iEvent;
            _wsReuest.Payload = payload;
        }

        public string GetPrivateRequest(string pKey, string sKey)
        {
            _signer = new Signer(sKey);
            _auth = new Auth() { Key = pKey, Method = "api_key", Sign = _signer.SignWs(_wsReuest.Channel, _wsReuest.Event, _wsReuest.Time.ToString()) };
            _wsReuest.Auth = _auth;

            return JsonConvert.SerializeObject(_wsReuest);
        }

        public string GetPublicRequest()
        {
            return JsonConvert.SerializeObject(_wsReuest);
        }
    }

    public partial class GateFuturesWsReuest
    {
        [JsonProperty("time")]
        public long Time { get; set; }

        [JsonProperty("channel")]
        public string Channel { get; set; }

        [JsonProperty("event")]
        public string Event { get; set; }

        [JsonProperty("payload")]
        public string[] Payload { get; set; }

        [JsonProperty("auth", NullValueHandling = NullValueHandling.Ignore)]
        public Auth Auth { get; set; }
    }

    public partial class Auth
    {
        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("KEY")]
        public string Key { get; set; }

        [JsonProperty("SIGN")]
        public string Sign { get; set; }
    }
}
