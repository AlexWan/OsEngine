using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.FTX.Entities
{
    public class AuthenticationRequest
    {
        [JsonProperty("op")]
        public OperationTypeEnum Operation { get; }

        [JsonProperty("args")]
        public Dictionary<string, string> Args { get; }

        public AuthenticationRequest(string apiKey, string signature, string time)
        {
            Operation = OperationTypeEnum.Login;
            Args = new Dictionary<string, string>()
            {
                {"key", apiKey },
                {"sign", signature },
                {"time", time }
            };
        }
    }
}
