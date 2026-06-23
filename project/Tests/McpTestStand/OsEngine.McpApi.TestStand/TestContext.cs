/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OsEngine.McpApi.TestStand
{
    /// <summary>
    /// Shared context and reporting helpers for the test stand.
    /// </summary>
    public class TestContext
    {
        public McpApiClient Client { get; private set; }

        public OsEngineProcessController ProcessController { get; }

        public string OsEnginePath { get; }

        public int Port { get; }

        public string ApiKey { get; }

        public int TimeoutSeconds { get; }

        public TestSecrets Secrets { get; }

        public List<TestResult> Results { get; } = new List<TestResult>();

        public int Passed { get; private set; }

        public int Failed { get; private set; }

        public TestContext(McpApiClient client, OsEngineProcessController processController,
            string osEnginePath, int port, string apiKey, int timeoutSeconds, TestSecrets secrets)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
            ProcessController = processController ?? throw new ArgumentNullException(nameof(processController));
            OsEnginePath = osEnginePath ?? throw new ArgumentNullException(nameof(osEnginePath));
            Port = port;
            ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            TimeoutSeconds = timeoutSeconds;
            Secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        }

        public void RestartOsEngine(string arguments)
        {
            ProcessController.Restart(arguments, TimeSpan.FromSeconds(TimeoutSeconds));
            Client = ProcessController.Client ?? throw new InvalidOperationException("MCP client is not available after restart");
        }

        public void StopOsEngine()
        {
            ProcessController.Stop();
        }

        public void PrintHeader()
        {
            Console.WriteLine("=== MCP API Test Stand ===");
            Console.WriteLine($"Base URL: {Client.BaseUrl}");
            Console.WriteLine();
        }

        public void PrintModuleHeader(string module)
        {
            Console.WriteLine($"--- {module} ---");
        }

        public void PrintRequest(string module, string method, object request)
        {
            Console.WriteLine($"[{module}] Method: {method}");
            Console.WriteLine("  Request:");
            PrintIndentedJson(MaskSecrets(Serialize(request)), "    ");
        }

        public void PrintResponse(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                Console.WriteLine("  Response: (empty)");
                return;
            }

            Console.WriteLine("  Response:");
            PrintMcpResponse(response, "    ");
        }

        public void RecordPass(string module, string method, string message)
        {
            string name = $"{module}.{method}";
            Results.Add(TestResult.Passed(name, message));
            Passed++;
            Console.WriteLine($"  Status:   PASS");
            Console.WriteLine();
        }

        public void RecordFail(string module, string method, string message)
        {
            string name = $"{module}.{method}";
            Results.Add(TestResult.Failed(name, message));
            Failed++;
            Console.WriteLine($"  Status:   FAIL - {message}");
            Console.WriteLine();
        }

        public void PrintSummary(TimeSpan elapsed)
        {
            Console.WriteLine("--- Module Summary ---");

            var moduleStats = new Dictionary<string, int[]>();

            foreach (TestResult result in Results)
            {
                string module = result.Name.Contains(".") ? result.Name.Substring(0, result.Name.IndexOf('.')) : "Unknown";

                if (!moduleStats.TryGetValue(module, out int[]? stats))
                {
                    stats = new int[2];
                    moduleStats[module] = stats;
                }

                if (result.Success)
                {
                    stats[0]++;
                }
                else
                {
                    stats[1]++;
                }
            }

            foreach (var pair in moduleStats)
            {
                int passed = pair.Value[0];
                int failed = pair.Value[1];
                int total = passed + failed;
                Console.WriteLine($"{pair.Key}: {passed}/{total} passed" + (failed > 0 ? $" ({failed} failed)" : ""));
            }

            Console.WriteLine();
            Console.WriteLine($"Total: {Passed}/{Passed + Failed} passed" + (Failed > 0 ? $" ({Failed} failed)" : "") + $" in {elapsed.TotalSeconds:F1}s");
        }

        private static string Serialize(object value)
        {
            try
            {
                return JsonSerializer.Serialize(value, new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
            }
            catch
            {
                return value?.ToString() ?? "null";
            }
        }

        private static void PrintIndentedJson(string json, string indent)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    string formatted = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });

                    foreach (string line in formatted.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
                    {
                        Console.WriteLine(indent + line);
                    }
                }
            }
            catch
            {
                Console.WriteLine(indent + json);
            }
        }

        private static void PrintMcpResponse(string response, string indent)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(response))
                {
                    JsonElement root = document.RootElement;

                    if (root.ValueKind == JsonValueKind.Object
                        && root.TryGetProperty("Content", out JsonElement contentElement)
                        && contentElement.ValueKind == JsonValueKind.Array)
                    {
                        bool isError = root.TryGetProperty("IsError", out JsonElement isErrorElement)
                            && isErrorElement.ValueKind == JsonValueKind.True;

                        Console.WriteLine(indent + "{");
                        Console.WriteLine(indent + "  \"Content\": [");

                        for (int i = 0; i < contentElement.GetArrayLength(); i++)
                        {
                            JsonElement item = contentElement[i];
                            Console.WriteLine(indent + "    {");

                            if (item.TryGetProperty("Type", out JsonElement typeElement)
                                && typeElement.ValueKind == JsonValueKind.String)
                            {
                                Console.WriteLine(indent + $"      \"Type\": \"{typeElement.GetString()}\",");
                            }

                            if (item.TryGetProperty("Text", out JsonElement textElement)
                                && textElement.ValueKind == JsonValueKind.String)
                            {
                                string? text = textElement.GetString();

                                if (!string.IsNullOrEmpty(text) && IsValidJson(text))
                                {
                                    Console.WriteLine(indent + "      \"Text\":");
                                    PrintIndentedJson(text, indent + "        ");
                                }
                                else
                                {
                                    Console.WriteLine(indent + $"      \"Text\": \"{text}\"");
                                }
                            }

                            Console.WriteLine(indent + "    }" + (i < contentElement.GetArrayLength() - 1 ? "," : ""));
                        }

                        Console.WriteLine(indent + "  ],");
                        Console.WriteLine(indent + $"  \"IsError\": {isError.ToString().ToLowerInvariant()}");
                        Console.WriteLine(indent + "}");
                        return;
                    }

                    PrintIndentedJson(response, indent);
                }
            }
            catch
            {
                Console.WriteLine(indent + response);
            }
        }

        private static bool IsValidJson(string text)
        {
            try
            {
                JsonDocument.Parse(text);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string MaskSecrets(string json)
        {
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    JsonElement masked = MaskSecrets(document.RootElement);

                    return JsonSerializer.Serialize(masked, new JsonSerializerOptions
                    {
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                }
            }
            catch
            {
                return json;
            }
        }

        private static JsonElement MaskSecrets(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    {
                        using (var stream = new System.IO.MemoryStream())
                        {
                            using (var writer = new Utf8JsonWriter(stream))
                            {
                                writer.WriteStartObject();

                                foreach (JsonProperty property in element.EnumerateObject())
                                {
                                    writer.WritePropertyName(property.Name);

                                    if (IsSensitivePropertyName(property.Name))
                                    {
                                        writer.WriteStringValue("***");
                                    }
                                    else
                                    {
                                        JsonElement maskedValue = MaskSecrets(property.Value);
                                        maskedValue.WriteTo(writer);
                                    }
                                }

                                writer.WriteEndObject();
                            }

                            stream.Position = 0;

                            using (JsonDocument doc = JsonDocument.Parse(stream))
                            {
                                return doc.RootElement.Clone();
                            }
                        }
                    }

                case JsonValueKind.Array:
                    {
                        using (var stream = new System.IO.MemoryStream())
                        {
                            using (var writer = new Utf8JsonWriter(stream))
                            {
                                writer.WriteStartArray();

                                foreach (JsonElement item in element.EnumerateArray())
                                {
                                    JsonElement maskedItem = MaskSecrets(item);
                                    maskedItem.WriteTo(writer);
                                }

                                writer.WriteEndArray();
                            }

                            stream.Position = 0;

                            using (JsonDocument doc = JsonDocument.Parse(stream))
                            {
                                return doc.RootElement.Clone();
                            }
                        }
                    }

                default:
                    return element.Clone();
            }
        }

        private static bool IsSensitivePropertyName(string name)
        {
            string lower = name.ToLowerInvariant();

            return lower.Contains("token")
                || lower.Contains("key")
                || lower.Contains("secret")
                || lower.Contains("password");
        }
    }
}
