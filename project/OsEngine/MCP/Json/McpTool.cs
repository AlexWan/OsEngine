/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Text.Json.Serialization;

namespace OsEngine.MCP.Json
{
    /// <summary>
    /// Anthropic MCP tool descriptor.
    /// </summary>
    public class McpTool
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("inputSchema")]
        public object InputSchema { get; set; }
    }
}
