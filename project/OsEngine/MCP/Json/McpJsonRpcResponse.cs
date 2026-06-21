/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Text.Json.Serialization;

namespace OsEngine.MCP.Json
{
    /// <summary>
    /// JSON-RPC 2.0 response.
    /// </summary>
    public class McpJsonRpcResponse
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; }

        [JsonPropertyName("result")]
        public object Result { get; set; }

        [JsonPropertyName("error")]
        public McpJsonRpcError Error { get; set; }

        [JsonPropertyName("id")]
        public object Id { get; set; }
    }
}
