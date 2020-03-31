using System.Collections.Generic;

namespace OsEngine.Market.Servers.Binance.Spot.BinanceSpotEntity
{

    public class Datas
    {
        public long lastUpdateId { get; set; }
        public List<List<object>> bids { get; set; }
        public List<List<object>> asks { get; set; }
    }

    public class DepthResponse
    {
        public string stream { get; set; }
        public Datas data { get; set; }
    }
}