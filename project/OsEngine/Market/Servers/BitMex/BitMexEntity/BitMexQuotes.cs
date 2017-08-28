using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.BitMex.BitMexEntity
{
    public class TypesQ
    {
        public string symbol { get; set; }
        public string bids { get; set; }
        public string asks { get; set; }
        public string timestamp { get; set; }
    }

    public class ForeignKeysQ
    {
        public string symbol { get; set; }
    }

    public class AttributesQ
    {
        public string symbol { get; set; }
    }

    public class DatumQ
    {
        public string symbol { get; set; }
        public List<List<decimal>> bids { get; set; }
        public List<List<decimal>> asks { get; set; }
        public string timestamp { get; set; }
    }

    public class FilterQ
    {
        public string symbol { get; set; }
    }

    public class BitMexQuotes
    {
        public string table { get; set; }
        public List<string> keys { get; set; }
        public TypesQ types { get; set; }
        public ForeignKeysQ foreignKeys { get; set; }
        public AttributesQ attributes { get; set; }
        public string action { get; set; }
        public List<DatumQ> data { get; set; }
        public FilterQ filter { get; set; }
    }
}
