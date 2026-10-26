/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace OsEngine.IndicatorMarketDepth.TestStand
{
    public class McpApiClient : IDisposable
    {
        private const string ProtocolVersion = "2024-11-05";

        private readonly HttpClient _httpClient;

        private string _sessionId;

        private readonly object _sessionLocker = new object();

        public string BaseUrl { get; }

        public string ApiKey { get; }

        private string RpcPath => "/api/v2/mcp";

        public McpApiClient(string baseUrl, string apiKey)
        {
            BaseUrl = baseUrl.TrimEnd('/');
            ApiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(30);
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
        }

        public string SendRequest(string method, object parameters)
        {
            string responseJson = SendRaw(method, parameters);

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
                    return NormalizeWrapper(resultElement.GetRawText());
                }

                return "null";
            }
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
                        clientInfo = new { name = "OsEngine.IndicatorMarketDepth.TestStand", version = "1.0.0" }
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

        private string SendRaw(string method, object parameters)
        {
            object request = new
            {
                jsonrpc = "2.0",
                method = method,
                @params = parameters,
                id = Guid.NewGuid().ToString()
            };

            string json = JsonSerializer.Serialize(request);

            if (method != "initialize")
            {
                EnsureSession();
            }

            using (StringContent content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl + RpcPath) { Content = content })
            {
                httpRequest.Headers.Add("Accept", "application/json, text/event-stream");

                lock (_sessionLocker)
                {
                    if (_sessionId != null)
                    {
                        httpRequest.Headers.Add("Mcp-Session-Id", _sessionId);
                        httpRequest.Headers.Add("MCP-Protocol-Version", ProtocolVersion);
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
                            // ignore
                        }

                        throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {errorBody}");
                    }

                    CaptureSession(response);
                    return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
            }
        }

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
                clientInfo = new { name = "OsEngine.IndicatorMarketDepth.TestStand", version = "1.0.0" }
            });
        }

        private void CaptureSession(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("Mcp-Session-Id", out System.Collections.Generic.IEnumerable<string> values))
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

        private static string NormalizeWrapper(string resultJson)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    JsonElement root = document.RootElement;

                    if (root.ValueKind == JsonValueKind.Object
                        && root.TryGetProperty("content", out JsonElement content)
                        && content.ValueKind == JsonValueKind.Array)
                    {
                        System.Collections.Generic.List<object> items = new System.Collections.Generic.List<object>();

                        foreach (JsonElement item in content.EnumerateArray())
                        {
                            string type = item.TryGetProperty("type", out JsonElement t) ? t.GetString() ?? "text" : "text";
                            string text = item.TryGetProperty("text", out JsonElement x) ? x.GetString() ?? string.Empty : string.Empty;
                            items.Add(new { Type = type, Text = text });
                        }

                        bool isError = root.TryGetProperty("isError", out JsonElement e) && e.ValueKind == JsonValueKind.True;

                        return JsonSerializer.Serialize(new { Content = items, IsError = isError });
                    }
                }
            }
            catch
            {
                // ignore
            }

            return resultJson;
        }

        public void Dispose()
        {
            try
            {
                _httpClient?.Dispose();
            }
            catch
            {
                // ignore
            }
        }
    }
}
