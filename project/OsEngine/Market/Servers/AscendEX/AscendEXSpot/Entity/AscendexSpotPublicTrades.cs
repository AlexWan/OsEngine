using System.Collections.Generic;

namespace OsEngine.Market.Servers.AscendexSpot.Entity
{
    class AscendexSpotPublicTradesResponse
    {
        public string m { get; set; }           // "trades"
        public string symbol { get; set; }      // "ASD/USDT"
        public List<AscendexSpotPublicTradeItem> data { get; set; }
    }

    class AscendexSpotPublicTradeItem
    {
        public string seqnum { get; set; }        // the sequence number of the trade record " 144115188077966308"
        public string p { get; set; }             // price "0.068600"
        public string q { get; set; }             // volume/quantity "100.000"
        public string ts { get; set; }            // the UTC timestamp in milliseconds  "1573069903254"
        public string bm { get; set; }            // is true = sell, false = buy 
    }
}