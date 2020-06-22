using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.GateIo.Futures.Response
{
    public class GetCandlestickResponse
    {
        public string security;

        /// <summary>
        /// Response status
        /// </summary>
        public string status;

        /// <summary>
        /// The timestamp (millisecond) when API respond
        /// </summary>
        public long ts;

        public GfCandle[] candles;

        public string timeFrame;
    }

        public partial class GfCandle
    {
        [JsonProperty("t")]
        public long T { get; set; }

        [JsonProperty("v")]
        public decimal V { get; set; }

        [JsonProperty("c")]
        public string C { get; set; }

        [JsonProperty("h")]
        public string H { get; set; }

        [JsonProperty("l")]
        public string L { get; set; }

        [JsonProperty("o")]
        public string O { get; set; }
    }
}
