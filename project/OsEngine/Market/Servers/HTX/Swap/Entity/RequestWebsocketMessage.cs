namespace OsEngine.Market.Servers.HTX.Swap.Entity
{
    public class WebSocketAuthenticationRequestFutures
    {
        public string op { get { return "auth"; } }
        public string type { get { return "api"; } }

        public string AccessKeyId;
        public string SignatureMethod { get { return "HmacSHA256"; } }
        public string SignatureVersion { get { return "2"; } }
        public string Timestamp;
        public string Signature;
    }
}
