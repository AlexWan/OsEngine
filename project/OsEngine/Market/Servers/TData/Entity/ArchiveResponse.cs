/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OsEngine.Market.Servers.TData.Entity
{
    public class ArchiveResponse
    {
        [JsonPropertyName("lastModified")]
        public string LastModified { get; set; }

        [JsonPropertyName("year")]
        public string Year { get; set; }

        [JsonPropertyName("entries")]
        public List<ArchiveEntry> Entries { get; set; }
    }

    public class ArchiveEntry
    {
        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("uri")]
        public string Uri { get; set; }

        [JsonPropertyName("sizeTxt")]
        public string SizeTxt { get; set; }
    }
}
