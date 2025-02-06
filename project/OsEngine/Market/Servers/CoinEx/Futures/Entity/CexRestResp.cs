using System.Net.Http;

namespace OsEngine.Market.Servers.CoinEx.Futures.Entity
{
    struct CexRestResp
    {
        // Common Response
        public string result { get; set; }

        public void EnsureSuccess()
        {
            if (result != "pong")
            {
                throw new HttpRequestException($"REST error: {result}");
            }
        }
    }
}
