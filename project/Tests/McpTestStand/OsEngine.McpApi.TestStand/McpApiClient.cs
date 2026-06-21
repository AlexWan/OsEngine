/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace OsEngine.McpApi.TestStand
{
    /// <summary>
    /// Synchronous HTTP client for OsEngine MCP API.
    /// </summary>
    public class McpApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;

        public string BaseUrl { get; }
        public string ApiKey { get; }

        public McpApiClient(string baseUrl, string apiKey)
        {
            try
            {
                BaseUrl = baseUrl.TrimEnd('/');
                ApiKey = apiKey;
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

                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/mcp") { Content = content })
                using (HttpResponseMessage response = _httpClient.Send(httpRequest))
                {
                    response.EnsureSuccessStatusCode();
                    return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
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
                        return resultElement.GetRawText();
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
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/mcp") { Content = content };

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
                protocolVersion = "2024-11-05",
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
                var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/v1/events");
                request.Headers.Add("X-Api-Key", ApiKey);
                request.Headers.Add("Accept", "text/event-stream");

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
                        protocolVersion = "2024-11-05",
                        capabilities = new { },
                        clientInfo = new { name = "test-stand", version = "1.0.0" }
                    });

                    return;
                }
                catch (Exception error)
                {
                    lastError = error.Message;
                    Thread.Sleep(500);
                }
            }

            throw new TimeoutException($"MCP API did not become ready in {timeout}. Last error: {lastError}");
        }

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
