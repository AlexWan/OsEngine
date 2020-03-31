/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMex.BitMexEntity
{
    public class TypesQuote
    {
        public string symbol { get; set; }
        public string id { get; set; }
        public string side { get; set; }
        public string size { get; set; }
        public string price { get; set; }
    }

    public class ForeignKeysQuote
    {
        public string symbol { get; set; }
        public string side { get; set; }
    }

    public class AttributesQuote
    {
        public string symbol { get; set; }
        public string id { get; set; }
    }

    public class DatumQuote
    {
        public string symbol { get; set; }
        public long id { get; set; }
        public string side { get; set; }
        public int size { get; set; }
        public string price { get; set; }
    }

    public class FilterQuote
    {
        public string symbol { get; set; }
    }

    public class BitMexQuotes
    {
        public string table { get; set; }
        public List<string> keys { get; set; }
        public TypesQuote types { get; set; }
        public ForeignKeysQuote foreignKeys { get; set; }
        public AttributesQuote attributes { get; set; }
        public string action { get; set; }
        public List<DatumQuote> data { get; set; }
        public FilterQuote filter { get; set; }
    }
}
