using Newtonsoft.Json;

namespace OsEngine.Market.Servers.FTX.Entities
{
    public class AuthenticationRequest
    {
        [JsonProperty("op")]
        public OperationTypeEnum Operation { get; }

        [JsonProperty("args")]
        public AuthenticationArgs Args { get; }

        public AuthenticationRequest(string apiKey, string signature, long time)
        {
            Operation = OperationTypeEnum.Login;
            Args = new AuthenticationArgs
            {
                Key = apiKey,
                Sign = signature,
                Time = time
            };
        }

        public class AuthenticationArgs
        {
            [JsonProperty("key")]
            public string Key { get; set; }

            [JsonProperty("sign")]
            public string Sign { get; set; }

            [JsonProperty("time")]
            public long Time { get; set; }
        }
    }
}
