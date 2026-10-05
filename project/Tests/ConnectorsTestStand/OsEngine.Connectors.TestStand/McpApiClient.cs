/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace OsEngine.Connectors.TestStand
{
    /// <summary>
    /// Synchronous HTTP client for OsEngine MCP API.
    /// Supports both transports via the streamableHttp flag:
    /// - v1: POST /api/v1/mcp (plain JSON-RPC, PascalCase wrapper);
    /// - v2: /api/v2/mcp (Streamable HTTP: session, MCP-Protocol-Version, camelCase).
    /// High-level methods (SendRequest/ToolsCall/...) normalize the v2 camelCase
    /// wrapper back to PascalCase so tool calls are transport-agnostic.
    /// </summary>
    public class McpApiClient : IDisposable
    {
        private const string ProtocolVersion = "2024-11-05";

        private readonly HttpClient _httpClient;
        private readonly bool _streamableHttp;
        private string _sessionId;
        private readonly object _sessionLocker = new object();

        public string BaseUrl { get; }
        public string ApiKey { get; }

        public bool StreamableHttp => _streamableHttp;

        private string RpcPath => _streamableHttp ? "/api/v2/mcp" : "/api/v1/mcp";

        public McpApiClient(string baseUrl, string apiKey, bool streamableHttp = true)
        {
            try
            {
                BaseUrl = baseUrl.TrimEnd('/');
                ApiKey = apiKey;
                _streamableHttp = streamableHttp;
                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
            }
            catch (Exception error)
            {
                throw new InvalidOperationException($"Failed to create MCP API client: {error}");
            }
        }

        public string SendRaw(string method, object parameters)
        {
            try
            {
                var request = new
                {
                    jsonrpc = "2.0",
                    method = method,
                    @params = parameters,
                    id = Guid.NewGuid().ToString()
                };

                string json = JsonSerializer.Serialize(request);

                if (_streamableHttp && method != "initialize")
                {
                    EnsureSession();
                }

                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl + RpcPath) { Content = content })
                {
                    httpRequest.Headers.Add("Accept", "application/json, text/event-stream");

                    if (_streamableHttp && method != "initialize")
                    {
                        lock (_sessionLocker)
                        {
                            if (_sessionId != null)
                            {
                                httpRequest.Headers.Add("Mcp-Session-Id", _sessionId);
                                httpRequest.Headers.Add("MCP-Protocol-Version", ProtocolVersion);
                            }
                        }
                    }

                    using (HttpResponseMessage response = _httpClient.Send(httpRequest))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            string errorBody = string.Empty;
                            try
                            {
                                errorBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            }
                            catch
                            {
                                // тело ошибки необязательно
                            }

                            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {errorBody}");
                        }

                        CaptureSession(response);
                        return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    }
                }
            }
            catch (Exception error)
            {
                throw new InvalidOperationException($"SendRaw failed for method '{method}': {error.Message}", error);
            }
        }

        public string SendRequest(string method, object parameters)
        {
            string responseJson = SendRaw(method, parameters);

            try
            {
                using (JsonDocument document = JsonDocument.Parse(responseJson))
                {
                    JsonElement root = document.RootElement;

                    if (root.TryGetProperty("error", out JsonElement errorElement) && errorElement.ValueKind != JsonValueKind.Null)
                    {
                        string message = errorElement.TryGetProperty("message", out JsonElement messageElement)
                            ? messageElement.GetString() ?? "unknown"
                            : "unknown";

                        throw new InvalidOperationException($"MCP error: {message}");
                    }

                    if (root.TryGetProperty("result", out JsonElement resultElement))
                    {
                        string resultJson = resultElement.GetRawText();

                        if (_streamableHttp)
                        {
                            resultJson = NormalizeWrapper(resultJson);
                        }

                        return resultJson;
                    }

                    return "null";
                }
            }
            catch (Exception error)
            {
                throw new InvalidOperationException($"SendRequest failed for method '{method}': {error.Message}", error);
            }
        }

        public string Initialize()
        {
            return SendRequest("initialize", new
            {
                protocolVersion = ProtocolVersion,
                capabilities = new { },
                clientInfo = new { name = "OsEngine.Connectors.TestStand", version = "1.0.0" }
            });
        }

        public string ToolsList()
        {
            return SendRequest("tools/list", new { });
        }

        public string ToolsCall(string name, object arguments)
        {
            return SendRequest("tools/call", new { name = name, arguments = arguments });
        }

        public void WaitForReady(TimeSpan timeout)
        {
            DateTime deadline = DateTime.Now.Add(timeout);
            string lastError = string.Empty;

            while (DateTime.Now < deadline)
            {
                try
                {
                    SendRequest("initialize", new
                    {
                        protocolVersion = ProtocolVersion,
                        capabilities = new { },
                        clientInfo = new { name = "test-stand", version = "1.0.0" }
                    });

                    return;
                }
                catch (Exception error)
                {
                    lastError = error.Message;

                    if (lastError.Contains("Encryptor is locked"))
                    {
                        return;
                    }

                    Thread.Sleep(500);
                }
            }

            throw new TimeoutException($"MCP API did not become ready in {timeout}. Last error: {lastError}");
        }

        #region Private helpers

        private void EnsureSession()
        {
            lock (_sessionLocker)
            {
                if (_sessionId != null)
                {
                    return;
                }
            }

            SendRaw("initialize", new
            {
                protocolVersion = ProtocolVersion,
                capabilities = new { },
                clientInfo = new { name = "test-stand", version = "1.0.0" }
            });
        }

        private void CaptureSession(HttpResponseMessage response)
        {
            if (!_streamableHttp)
            {
                return;
            }

            if (response.Headers.TryGetValues("Mcp-Session-Id", out var values))
            {
                foreach (string value in values)
                {
                    lock (_sessionLocker)
                    {
                        _sessionId = value;
                    }
                    return;
                }
            }
        }

        // v2 отдаёт camelCase-обёртку, тесты парсят PascalCase — приводим к единому виду
        private static string NormalizeWrapper(string resultJson)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    if (root.ValueKind != JsonValueKind.Object)
                    {
                        return resultJson;
                    }

                    if (root.TryGetProperty("content", out JsonElement content)
                        && content.ValueKind == JsonValueKind.Array)
                    {
                        var items = new System.Collections.Generic.List<object>();

                        foreach (JsonElement item in content.EnumerateArray())
                        {
                            string type = item.TryGetProperty("type", out JsonElement t) ? t.GetString() ?? "text" : "text";
                            string text = item.TryGetProperty("text", out JsonElement x) ? x.GetString() ?? string.Empty : string.Empty;
                            items.Add(new { Type = type, Text = text });
                        }

                        bool isError = root.TryGetProperty("isError", out JsonElement e) && e.ValueKind == JsonValueKind.True;

                        return JsonSerializer.Serialize(new { Content = items, IsError = isError });
                    }

                    if (root.TryGetProperty("tools", out JsonElement tools))
                    {
                        return JsonSerializer.Serialize(new { Tools = tools });
                    }
                }
            }
            catch
            {
                // нормализация не должна ломать тест — вернём как есть
            }

            return resultJson;
        }

        #endregion

        public void Dispose()
        {
            try
            {
                _httpClient?.Dispose();
            }
            catch (Exception error)
            {
                Console.WriteLine($"Failed to dispose HttpClient: {error.Message}");
            }
        }
    }
}
