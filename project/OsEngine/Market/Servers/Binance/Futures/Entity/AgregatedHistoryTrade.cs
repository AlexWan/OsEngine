using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Binance.Futures.Entity
{
    public partial class AgregatedHistoryTrade
    {
        /*
        https://binance-docs.github.io/apidocs/spot/en/#compressed-aggregate-trades-list

        {
            "a": 26129,         // Aggregate tradeId
            "p": "0.01633102",  // Price
            "q": "4.70443515",  // Quantity
            "f": 27781,         // First tradeId
            "l": 27781,         // Last tradeId
            "T": 1498793709153, // Timestamp
            "m": true,          // Was the buyer the maker?
            "M": true           // Was the trade the best price match?
        }
        */

        [JsonProperty("a")]
        public long A { get; set; }

        [JsonProperty("p")]
        public string P { get; set; }

        [JsonProperty("q")]
        public string Q { get; set; }

        [JsonProperty("f")]
        public long F { get; set; }

        [JsonProperty("l")]
        public long L { get; set; }

        [JsonProperty("T")]
        public long T { get; set; }

        [JsonProperty("m")]
        public bool m { get; set; }

        [JsonProperty("M")]
        public bool M { get; set; }
    }
}
