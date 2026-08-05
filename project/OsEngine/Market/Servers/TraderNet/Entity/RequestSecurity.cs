using Newtonsoft.Json;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.TraderNet.Entity
{
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]

    public class RequestSecurity
    {
        public int? take;

        public int? skip;

        public List<Sort> sort;

        public Filter filter { get; set; }

        public class Sort
        {
            public string field { get; set; }
            public string dir { get; set; }
        }

        public class Filter
        {
            public List<FilterItem> filters { get; set; }
        }

        public class FilterItem
        {
            public string field { get; set; }
            public string @operator { get; set; }
            public string value { get; set; }
        }
    }

}
