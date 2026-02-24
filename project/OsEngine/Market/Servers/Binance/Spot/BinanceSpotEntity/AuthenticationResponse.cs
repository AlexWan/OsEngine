namespace OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity
{
    public class AuthenticationResponse
    {
        public string id { get; set; }
        public string status { get; set; }
        public AuthenticationResult result { get; set; }
    }

    public class AuthenticationResult
    {
        public string apiKey { get; set; }

        public string authorizedSince { get; set; }

        public string connectedSince { get; set; }

        public string returnRateLimits { get; set; }

        public string serverTime { get; set; }

        public string userDataStream { get; set; }
    }
}
