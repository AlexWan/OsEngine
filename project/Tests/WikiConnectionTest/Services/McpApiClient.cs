/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple.pdf
*/

using System.Text;
using System.Text.Json;

namespace WikiConnectionTest.Services
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
            BaseUrl = baseUrl.TrimEnd('/');
            ApiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
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

        public string Initialize()
        {
            return SendRequest("initialize", new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "WikiConnectionTest", version = "1.0.0" }
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
                    Initialize();
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
