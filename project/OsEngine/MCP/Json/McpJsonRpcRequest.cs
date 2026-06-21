/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Text.Json;
using System.Text.Json.Serialization;

namespace OsEngine.MCP.Json
{
    /// <summary>
    /// JSON-RPC 2.0 request.
    /// </summary>
    public class McpJsonRpcRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; }

        [JsonPropertyName("params")]
        public JsonElement Params { get; set; }

        [JsonPropertyName("id")]
        public object Id { get; set; }
    }
}
