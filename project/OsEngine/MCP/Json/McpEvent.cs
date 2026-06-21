/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Text.Json.Serialization;

namespace OsEngine.MCP.Json
{
    /// <summary>
    /// SSE event payload wrapper.
    /// </summary>
    public class McpEvent
    {
        [JsonPropertyName("event")]
        public string Event { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("payload")]
        public object Payload { get; set; }
    }
}
