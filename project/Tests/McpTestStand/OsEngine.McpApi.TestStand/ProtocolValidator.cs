/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace OsEngine.McpApi.TestStand
{
    /// <summary>
    /// Strict JSON-RPC 2.0 / MCP protocol validator for the test stand.
    /// Mirrors the official MCP TypeScript SDK response schemas
    /// (z.union of .strict() result/error objects) so the stand sees
    /// responses exactly like strict clients (Claude Code) do.
    /// Never throws, never fails a test: only collects textual issues.
    /// </summary>
    public static class ProtocolValidator
    {
        #region Public methods

        public static List<string> ValidateRequest(string requestJson)
        {
            List<string> issues = new List<string>();

            if (!TryParseObject(requestJson, out JsonElement root))
            {
                issues.Add("request body is not a JSON object");
                return issues;
            }

            if (!root.TryGetProperty("jsonrpc", out JsonElement jsonrpc)
                || jsonrpc.ValueKind != JsonValueKind.String
                || jsonrpc.GetString() != "2.0")
            {
                issues.Add("request: 'jsonrpc' missing or not \"2.0\"");
            }

            if (!root.TryGetProperty("method", out JsonElement method)
                || method.ValueKind != JsonValueKind.String
                || string.IsNullOrEmpty(method.GetString()))
            {
                issues.Add("request: 'method' missing or not a string");
            }

            if (root.TryGetProperty("params", out JsonElement parameters)
                && parameters.ValueKind != JsonValueKind.Object
                && parameters.ValueKind != JsonValueKind.Array)
            {
                issues.Add("request: 'params' must be an object or an array");
            }

            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (property.Name != "jsonrpc"
                    && property.Name != "method"
                    && property.Name != "params"
                    && property.Name != "id")
                {
                    issues.Add($"request: unexpected member '{property.Name}'");
                }
            }

            return issues;
        }

        public static List<string> ValidateResponse(string requestJson, string responseJson)
        {
            List<string> issues = new List<string>();

            if (!TryParseObject(responseJson, out JsonElement root))
            {
                issues.Add("response body is not a JSON object");
                return issues;
            }

            string requestId = ExtractId(requestJson);
            string method = ExtractMethod(requestJson);

            if (!root.TryGetProperty("jsonrpc", out JsonElement jsonrpc)
                || jsonrpc.ValueKind != JsonValueKind.String
                || jsonrpc.GetString() != "2.0")
            {
                issues.Add("response: 'jsonrpc' missing or not \"2.0\"");
            }

            if (!root.TryGetProperty("id", out JsonElement id))
            {
                issues.Add("response: 'id' missing");
            }
            else if (!string.IsNullOrEmpty(requestId) && id.ToString() != requestId)
            {
                issues.Add($"response: 'id' mismatch, expected '{requestId}', got '{id}'");
            }

            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (property.Name != "jsonrpc"
                    && property.Name != "id"
                    && property.Name != "result"
                    && property.Name != "error")
                {
                    issues.Add($"response: unexpected member '{property.Name}'");
                }
            }

            bool hasResult = root.TryGetProperty("result", out JsonElement result);
            bool hasError = root.TryGetProperty("error", out JsonElement error);

            if (hasResult && hasError)
            {
                // JSON-RPC 2.0, section 5: both members MUST NOT be included;
                // MCP SDK strict union rejects such responses with invalid_union
                issues.Add("response: both 'result' and 'error' members present (invalid_union in strict clients)");
            }
            else if (!hasResult && !hasError)
            {
                issues.Add("response: neither 'result' nor 'error' member present");
            }

            if (hasError && error.ValueKind != JsonValueKind.Null)
            {
                ValidateErrorObject(error, issues);
            }

            if (hasResult && !string.IsNullOrEmpty(method))
            {
                ValidateMcpResult(method, result, issues);
            }

            return issues;
        }

        public static List<string> ValidateSseEvent(string eventName, string dataJson)
        {
            List<string> issues = new List<string>();

            if (string.IsNullOrEmpty(eventName))
            {
                issues.Add("SSE frame: 'event:' line missing");
            }

            if (!TryParseObject(dataJson, out JsonElement root))
            {
                issues.Add("SSE frame: 'data:' is not a JSON object");
                return issues;
            }

            if (!root.TryGetProperty("event", out JsonElement eventField)
                || eventField.ValueKind != JsonValueKind.String)
            {
                issues.Add("SSE payload: 'event' missing or not a string");
            }
            else if (!string.IsNullOrEmpty(eventName) && eventField.GetString() != eventName)
            {
                issues.Add($"SSE payload: 'event' mismatch, frame '{eventName}', payload '{eventField.GetString()}'");
            }

            if (!root.TryGetProperty("timestamp", out _))
            {
                issues.Add("SSE payload: 'timestamp' missing");
            }

            if (!root.TryGetProperty("payload", out _))
            {
                issues.Add("SSE payload: 'payload' missing");
            }

            return issues;
        }

        #endregion

        #region Private methods

        private static void ValidateErrorObject(JsonElement error, List<string> issues)
        {
            if (error.ValueKind != JsonValueKind.Object)
            {
                issues.Add("response: 'error' is not an object");
                return;
            }

            if (!error.TryGetProperty("code", out JsonElement code)
                || code.ValueKind != JsonValueKind.Number
                || !code.TryGetInt32(out _))
            {
                issues.Add("response: 'error.code' missing or not an integer");
            }

            if (!error.TryGetProperty("message", out JsonElement message)
                || message.ValueKind != JsonValueKind.String)
            {
                issues.Add("response: 'error.message' missing or not a string");
            }
        }

        private static void ValidateMcpResult(string method, JsonElement result, List<string> issues)
        {
            if (result.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (method == "initialize")
            {
                RequireProperty(result, "protocolVersion", issues);
                RequireProperty(result, "capabilities", issues);
                RequireProperty(result, "serverInfo", issues);
            }
            else if (method == "tools/list")
            {
                // спецификация MCP ожидает строго camelCase-ключи
                RequireProperty(result, "tools", issues);
                RejectProperty(result, "Tools", issues);
            }
            else if (method == "tools/call")
            {
                RequireProperty(result, "content", issues);
                RequireProperty(result, "isError", issues);
                RejectProperty(result, "Content", issues);
                RejectProperty(result, "IsError", issues);

                if (result.TryGetProperty("content", out JsonElement content)
                    && content.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in content.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        RequireProperty(item, "type", issues);
                        RequireProperty(item, "text", issues);
                        RejectProperty(item, "Type", issues);
                        RejectProperty(item, "Text", issues);
                    }
                }
            }
        }

        private static void RequireProperty(JsonElement obj, string name, List<string> issues)
        {
            if (!obj.TryGetProperty(name, out _))
            {
                issues.Add($"result: expected member '{name}' is missing");
            }
        }

        private static void RejectProperty(JsonElement obj, string name, List<string> issues)
        {
            if (obj.TryGetProperty(name, out _))
            {
                issues.Add($"result: unexpected member '{name}' (MCP expects camelCase)");
            }
        }

        private static bool TryParseObject(string json, out JsonElement root)
        {
            root = default;

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    if (document.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        return false;
                    }

                    root = document.RootElement.Clone();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string ExtractId(string requestJson)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(requestJson))
                {
                    if (document.RootElement.TryGetProperty("id", out JsonElement id))
                    {
                        return id.ToString();
                    }
                }
            }
            catch
            {
                // not a validatable request
            }

            return string.Empty;
        }

        private static string ExtractMethod(string requestJson)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(requestJson))
                {
                    if (document.RootElement.TryGetProperty("method", out JsonElement method))
                    {
                        return method.GetString() ?? string.Empty;
                    }
                }
            }
            catch
            {
                // not a validatable request
            }

            return string.Empty;
        }

        #endregion
    }

    /// <summary>
    /// One protocol violation found by ProtocolValidator. Not a test failure.
    /// </summary>
    public class ProtocolViolation
    {
        public string Module = string.Empty;
        public string Method = string.Empty;
        public string Direction = string.Empty;
        public string Message = string.Empty;
    }
}
