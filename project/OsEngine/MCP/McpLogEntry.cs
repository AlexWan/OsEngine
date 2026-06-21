/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Text.Json.Serialization;

namespace OsEngine.MCP
{
    /// <summary>
    /// Single log entry returned by MCP API.
    /// </summary>
    public class McpLogEntry
    {
        [JsonPropertyName("time")]
        public DateTime Time { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}
