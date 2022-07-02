namespace OsEngine.Market.Servers.FTX.FtxApi
{
    public class ClientApiKeys
    {
        public string ApiKey { get; }

        public string ApiSecret { get; }

        public ClientApiKeys()
        {
            ApiKey = "";
            ApiSecret = "";
        }

        public ClientApiKeys(string apiKey, string apiSecret)
        {
            ApiKey = apiKey;
            ApiSecret = apiSecret;
        }

    }
}