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
        public bool M { get; set; }
    }
}
