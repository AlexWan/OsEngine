/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OsEngine.Market.Servers.TData.Entity
{
    public class RootResponse
    {
        [JsonPropertyName("lastModified")]
        public string LastModified { get; set; }

        [JsonPropertyName("instruments")]
        public Dictionary<string, TSecurityResponse> Instruments { get; set; }
    }

    public class TSecurityResponse
    {
        [JsonPropertyName("ticker")]
        public string Ticker { get; set; }

        [JsonPropertyName("classCode")]
        public string ClassCode { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("archives")]
        public List<Archive>? Archives { get; set; }
    }

    public class Archive
    {
        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("uri")]
        public string Uri { get; set; }

        [JsonPropertyName("sizeTxt")]
        public string SizeTxt { get; set; }
    }
}
