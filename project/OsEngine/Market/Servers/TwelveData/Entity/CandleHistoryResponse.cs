using System.Collections.Generic;

namespace OsEngine.Market.Servers.TwelveData.Entity
{
    public class CandleHistoryResponse
    {
        public Meta meta { get; set; }
        public List<ValueCandle> values { get; set; }
        public string status { get; set; }
    }

    public class Meta
    {
        public string symbol { get; set; }
        public string interval { get; set; }
        public string currency { get; set; }
        public string exchange_timezone { get; set; }
        public string exchange { get; set; }
        public string mic_code { get; set; }
        public string type { get; set; }
    }

    public class ValueCandle
    {
        public string datetime { get; set; }
        public string open { get; set; }
        public string high { get; set; }
        public string low { get; set; }
        public string close { get; set; }
        public string volume { get; set; }
    }
}
