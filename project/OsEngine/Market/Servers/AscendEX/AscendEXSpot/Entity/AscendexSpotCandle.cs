using System.Collections.Generic;

namespace OsEngine.Market.Servers.AscendexSpot.Entity
{
    public class AscendexSpotCandleResponse
    {
        public string code { get; set; }
        public List<AscendexSpotCandleEntry> data { get; set; }
    }

    public class AscendexSpotCandleEntry
    {
        public string m { get; set; }        // Type ("bar")
        public string s { get; set; }        //Symbol (e.g. BTC/USDT)
        public AscendexSpotCandleData data { get; set; }  // Response data
    }

    public class AscendexSpotCandleData
    {
        public string i { get; set; }        // Interval
        public string ts { get; set; }         // Timestamp (Unix ms)
        public string o { get; set; }        // Open
        public string c { get; set; }        // Close
        public string h { get; set; }        // High
        public string l { get; set; }        // Low
        public string v { get; set; }        // Volume
    }
}