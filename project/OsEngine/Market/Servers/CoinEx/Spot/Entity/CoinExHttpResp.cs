using System.Net.Http;

namespace OsEngine.Market.Servers.CoinEx.Spot.Entity
{
    struct CoinExHttpResp<T>
    {
        // https://docs.coinex.com/api/v2/guide#http-response-processing

        public int code { get; set; }

        public string message { get; set; }

        public T data { get; set; }

        public Pagination pagination { get; set; }

        public void EnsureSuccessStatusCode()
        {
            if (code != 0)
            {
                throw new HttpRequestException($"REST error: [{code}] {message}.");
            }
        }
    }
    struct Pagination
    {
        public int total { get; set; }

        public bool has_next { get; set; }
    }
}