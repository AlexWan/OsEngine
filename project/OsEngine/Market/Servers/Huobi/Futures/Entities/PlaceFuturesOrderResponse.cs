namespace OsEngine.Market.Servers.Huobi.Futures.Entities
{
    public class PlaceFuturesOrderResponse
    {
        public string status { get; set; }
        public long order_id { get; set; }
        public string order_id_str { get; set; }
        public int client_order_id { get; set; }
        public long ts { get; set; }
    }
}
