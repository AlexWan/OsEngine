/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple.pdf
*/

using System.Text.Json;

namespace WikiConnectionTest.Services
{
    /// <summary>
    /// High-level MCP API service for WikiConnectionTest.
    /// </summary>
    public class McpService
    {
        private readonly McpApiClient _client;

        public McpService(McpApiClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public void Initialize()
        {
            _client.Initialize();
        }

        public void ToolsList()
        {
            _client.ToolsList();
        }

        public void TerminalStop()
        {
            _client.ToolsCall("terminal_stop", new { });
        }

        public void ActivateServer(string type)
        {
            _client.ToolsCall("server_management_activate", new { type = type });
        }

        public JsonElement GetConnectorPermissions(string type)
        {
            string response = _client.ToolsCall("server_management_get_connector_permissions", new { type = type });
            return ExtractToolResult(response);
        }

        public int CreateInstance(string type)
        {
            string response = _client.ToolsCall("server_instance_create", new { type = type });
            JsonElement result = ExtractToolResult(response);

            if (!result.TryGetProperty("number", out JsonElement numberElement)
                || numberElement.ValueKind != JsonValueKind.Number
                || !numberElement.TryGetInt32(out int number)
                || number < 1)
            {
                throw new InvalidOperationException($"Failed to create a valid instance for {type}");
            }

            return number;
        }

        public void DeleteInstance(string type, int number)
        {
            _client.ToolsCall("server_instance_delete", new { type = type, number = number });
        }

        public List<(string Type, int Number, string Status)> GetServerList()
        {
            string response = _client.ToolsCall("server_management_get_list", new { });
            JsonElement result = ExtractToolResult(response);

            var servers = new List<(string, int, string)>();

            if (result.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in result.EnumerateArray())
                {
                    string? type = item.TryGetProperty("type", out JsonElement typeElement)
                        ? typeElement.GetString()
                        : null;

                    int number = item.TryGetProperty("number", out JsonElement numberElement)
                        && numberElement.ValueKind == JsonValueKind.Number
                        && numberElement.TryGetInt32(out int parsedNumber)
                        ? parsedNumber
                        : 0;

                    string? status = item.TryGetProperty("status", out JsonElement statusElement)
                        ? statusElement.GetString()
                        : null;

                    if (!string.IsNullOrEmpty(type))
                    {
                        servers.Add((type, number, status ?? string.Empty));
                    }
                }
            }

            return servers;
        }

        public List<(string Name, string Type)> GetServerParams(string type, int number)
        {
            string response = _client.ToolsCall("server_instance_get_params", new { type = type, number = number });
            JsonElement result = ExtractToolResult(response);

            var parameters = new List<(string, string)>();

            if (result.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in result.EnumerateArray())
                {
                    string? name = item.TryGetProperty("name", out JsonElement nameElement)
                        ? nameElement.GetString()
                        : null;

                    string? paramType = item.TryGetProperty("type", out JsonElement typeElement)
                        ? typeElement.GetString()
                        : null;

                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(paramType))
                    {
                        parameters.Add((name, paramType));
                    }
                }
            }

            return parameters;
        }

        public void SetParams(string type, int number, IEnumerable<KeyValuePair<string, object>> parameters)
        {
            var paramArray = parameters
                .Select(p => new { name = p.Key, value = p.Value })
                .ToArray();

            _client.ToolsCall("server_instance_set_params", new { type = type, number = number, parameters = paramArray });
        }

        public void ConnectInstance(string type, int number)
        {
            _client.ToolsCall("server_instance_connect", new { type = type, number = number });
        }

        public void DisconnectInstance(string type, int number)
        {
            _client.ToolsCall("server_instance_disconnect", new { type = type, number = number });
        }

        public string GetStatus(string type, int number)
        {
            string response = _client.ToolsCall("server_instance_get_status", new { type = type, number = number });
            JsonElement result = ExtractToolResult(response);

            if (!result.TryGetProperty("status", out JsonElement statusElement)
                || statusElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException($"get_status did not return a valid status for {type}");
            }

            return statusElement.GetString() ?? string.Empty;
        }

        public List<JsonElement> GetSecurities(string type, int number, bool reload = true)
        {
            if (reload)
            {
                try
                {
                    string response = _client.ToolsCall("server_instance_get_securities", new { type = type, number = number, reload = true });
                    return ParseSecurities(response, type);
                }
                catch (InvalidOperationException error)
                {
                    Console.WriteLine($"[MCP] get_securities with reload failed: {error.Message}. Retrying without reload.");
                }
            }

            string responseWithoutReload = _client.ToolsCall("server_instance_get_securities", new { type = type, number = number, reload = false });
            return ParseSecurities(responseWithoutReload, type);
        }

        private static List<JsonElement> ParseSecurities(string response, string type)
        {
            JsonElement result = ExtractToolResult(response);

            if (!result.TryGetProperty("securities", out JsonElement securitiesElement)
                || securitiesElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException($"get_securities did not return a securities array for {type}");
            }

            return securitiesElement.EnumerateArray().Select(e => e.Clone()).ToList();
        }

        private static JsonElement ExtractToolResult(string response)
        {
            using (JsonDocument document = JsonDocument.Parse(response))
            {
                JsonElement root = document.RootElement;

                if (!root.TryGetProperty("Content", out JsonElement contentElement)
                    || contentElement.ValueKind != JsonValueKind.Array
                    || contentElement.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException("MCP response Content is missing or empty");
                }

                if (root.TryGetProperty("IsError", out JsonElement isErrorElement)
                    && isErrorElement.ValueKind == JsonValueKind.True)
                {
                    string errorText = ExtractText(contentElement);
                    throw new InvalidOperationException($"MCP tool error: {errorText}");
                }

                JsonElement textElement = contentElement[0];

                if (!textElement.TryGetProperty("Text", out JsonElement textProperty)
                    || textProperty.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidOperationException("MCP response Content[0].Text is missing or not a string");
                }

                string resultJson = textProperty.GetString() ?? string.Empty;

                using (JsonDocument resultDocument = JsonDocument.Parse(resultJson))
                {
                    return resultDocument.RootElement.Clone();
                }
            }
        }

        private static string ExtractText(JsonElement contentElement)
        {
            foreach (JsonElement item in contentElement.EnumerateArray())
            {
                if (item.TryGetProperty("Text", out JsonElement textProperty)
                    && textProperty.ValueKind == JsonValueKind.String)
                {
                    return textProperty.GetString() ?? string.Empty;
                }
            }

            return string.Empty;
        }
    }
}
