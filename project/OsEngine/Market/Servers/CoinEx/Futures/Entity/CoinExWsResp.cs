using System.Net.WebSockets;

namespace OsEngine.Market.Servers.CoinEx.Futures.Entity
{
    struct CoinExWsResp<T>
    {
        // https://docs.coinex.com/api/v2/guide#ws-response-processing

        public long? id { get; set; }
        
        public string message { get; set; }
        
        public string method { get; set; }

        public long code { get; set; }
        
        public T data { get; set; }

        public void EnsureSuccessStatusCode()
        {
            if (code > 0)
            {
                throw new WebSocketException($"Web socket error: [{code}] {message}.");
            }
        }
    }
}