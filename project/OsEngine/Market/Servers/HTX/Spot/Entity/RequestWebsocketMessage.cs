namespace OsEngine.Market.Servers.HTX.Spot.Entity
{
    public class WebSocketAuthenticationRequestV2
    {
        public class Params
        {
            public string authType { get { return "api"; } }
            public string accessKey { get; set; }
            public string signatureMethod { get { return "HmacSHA256"; } }
            public string signatureVersion { get { return "2.1"; } }
            public string timestamp { get; set; }
            public string signature { get; set; }
        }

        public string action { get { return "req"; } }
        public string ch { get { return "auth"; } }
        public Params @params;

    }
}
