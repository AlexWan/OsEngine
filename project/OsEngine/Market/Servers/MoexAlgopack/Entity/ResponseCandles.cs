using System.Collections.Generic;
using Newtonsoft.Json;

namespace OsEngine.Market.Servers.MoexAlgopack.Entity
{
    public class ResponseCandles
    {
        public Candles candles { get; set; }
    }

    public class Candles
    {
        [JsonIgnore]
        public List<string> columns { get; set; }
        public List<List<string>> data {get; set;}
    }
}
