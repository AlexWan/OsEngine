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

namespace OsEngine.McpApi.TestStand
{
    /// <summary>
    /// Synchronous HTTP client for OsEngine MCP API.
    /// Supports both transports via the streamableHttp flag:
    /// - v1: POST /api/v1/mcp (plain JSON-RPC, PascalCase wrapper);
    /// - v2: /api/v2/mcp (Streamable HTTP: session, MCP-Protocol-Version, camelCase).
    /// High-level methods (SendRequest/ToolsCall/...) normalize the v2 camelCase
    /// wrapper back to PascalCase so tool tests are transport-agnostic.
    /// Raw methods (PostRaw/GetRaw/DeleteRaw) return the wire form for transport tests.
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
        private string SsePath => _streamableHttp ? "/api/v2/mcp" : "/api/v1/events";

        public McpApiClient(string baseUrl, string apiKey, bool streamableHttp = false)
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

        #region High-level (transport-agnostic, normalized)

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
                using (var document = JsonDocument.Parse(responseJson))
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

        public HttpResponseMessage SendNotificationRaw(string method, object parameters)
        {
            try
            {
                var request = new
                {
                    jsonrpc = "2.0",
                    method = method,
                    @params = parameters
                };

                string json = JsonSerializer.Serialize(request);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl + RpcPath) { Content = content };

                HttpResponseMessage response = _httpClient.Send(httpRequest);
                return response;
            }
            catch (Exception error)
            {
                throw new InvalidOperationException($"SendNotificationRaw failed for method '{method}': {error.Message}", error);
            }
        }

        public void SendNotification(string method, object parameters)
        {
            using (HttpResponseMessage response = SendNotificationRaw(method, parameters))
            {
                response.EnsureSuccessStatusCode();
            }
        }

        public string Initialize()
        {
            return SendRequest("initialize", new
            {
                protocolVersion = ProtocolVersion,
                capabilities = new { },
                clientInfo = new { name = "OsEngine.McpApi.TestStand", version = "1.0.0" }
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

        public HttpResponseMessage GetSseResponse()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + SsePath);
                request.Headers.Add("X-Api-Key", ApiKey);
                request.Headers.Add("Accept", "text/event-stream");

                if (_streamableHttp)
                {
                    lock (_sessionLocker)
                    {
                        if (_sessionId != null)
                        {
                            request.Headers.Add("Mcp-Session-Id", _sessionId);
                        }
                    }
                }

                HttpResponseMessage response = _httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                return response;
            }
            catch (Exception error)
            {
                throw new InvalidOperationException($"Failed to connect to SSE endpoint: {error.Message}", error);
            }
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

        #endregion

        #region Raw methods (for transport tests, wire format)

        public HttpResult PostRaw(string body, string sessionId = null, string protocolVersion = null)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + RpcPath)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Accept", "application/json, text/event-stream");

            if (sessionId != null)
            {
                request.Headers.Add("Mcp-Session-Id", sessionId);
            }

            if (protocolVersion != null)
            {
                request.Headers.Add("MCP-Protocol-Version", protocolVersion);
            }

            using (HttpResponseMessage response = _httpClient.Send(request))
            {
                return ReadResult(response);
            }
        }

        public HttpResult GetRaw(string sessionId = null)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + SsePath);
            request.Headers.Add("Accept", "text/event-stream");

            if (sessionId != null)
            {
                request.Headers.Add("Mcp-Session-Id", sessionId);
            }

            using (HttpResponseMessage response = _httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead))
            {
                return ReadResult(response);
            }
        }

        public HttpResult DeleteRaw(string sessionId)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, BaseUrl + RpcPath);

            if (sessionId != null)
            {
                request.Headers.Add("Mcp-Session-Id", sessionId);
            }

            using (HttpResponseMessage response = _httpClient.Send(request))
            {
                return ReadResult(response);
            }
        }

        // читает SSE-кадры (data:) из GET-стрима в течение maxWait, вызывая onDataFrame на каждый кадр.
        // readySignal (если задан) сигналится после первой прочитанной строки — т.е. когда сервер
        // зарегистрировал стрим и отправил приветствие (": connected").
        public void ReadSseData(string sessionId, TimeSpan maxWait, ManualResetEvent readySignal, Action<string> onDataFrame)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + SsePath);
            request.Headers.Add("Accept", "text/event-stream");
            request.Headers.Add("Mcp-Session-Id", sessionId);

            using (HttpResponseMessage response = _httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead))
            using (Stream stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                DateTime deadline = DateTime.Now.Add(maxWait);
                bool firstLine = true;

                while (DateTime.Now < deadline)
                {
                    string line = reader.ReadLine();

                    if (line == null)
                    {
                        break;
                    }

                    if (firstLine)
                    {
                        firstLine = false;
                        readySignal?.Set();
                    }

                    if (line.StartsWith("data: "))
                    {
                        onDataFrame?.Invoke(line.Substring("data: ".Length).Trim());
                    }
                }
            }
        }

        #endregion

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

        // v2 отдаёт camelCase-обёртку, тесты инструментов парсят PascalCase — приводим к единому виду
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

        private static HttpResult ReadResult(HttpResponseMessage response)
        {
            HttpResult result = new HttpResult
            {
                StatusCode = (int)response.StatusCode,
                Body = string.Empty,
                SessionId = null,
                ContentType = response.Content?.Headers?.ContentType?.MediaType ?? string.Empty
            };

            if (response.Headers.TryGetValues("Mcp-Session-Id", out var sessionValues))
            {
                foreach (string value in sessionValues)
                {
                    result.SessionId = value;
                    break;
                }
            }

            if (response.Content != null && response.Content.Headers.ContentType != null
                && response.Content.Headers.ContentType.MediaType != "text/event-stream")
            {
                using (StreamReader reader = new StreamReader(response.Content.ReadAsStreamAsync().GetAwaiter().GetResult(), Encoding.UTF8))
                {
                    result.Body = reader.ReadToEnd();
                }
            }

            return result;
        }

        #endregion

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

    public class HttpResult
    {
        public int StatusCode;
        public string Body = string.Empty;
        public string SessionId;
        public string ContentType = string.Empty;
    }
}
