

namespace OsEngine.Market.Servers.Bitfinex.Json
{
    public class BitfinexFuturesSubscriptionResponse
    {
        public string Event { get; set; }
        public string Channel { get; set; }
        public string ChanId { get; set; }
        public string Symbol { get; set; }
        public string Pair { get; set; }
    }

    class BitfinexFuturesAuthResponseWebSocket
    {
        public string Event { get; set; }
        public string Status { get; set; }
        public string ChanId { get; set; }
        public string UserId { get; set; }
        public string AuthId { get; set; }
        public string Msg { get; set; }
    }
}